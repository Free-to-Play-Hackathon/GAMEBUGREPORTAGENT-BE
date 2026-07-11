using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Time;
using GameBug.Application.QaWorkflow;
using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;
using GameBug.Domain.Trust;
using MediatR;

namespace GameBug.Application.QaWorkflow.MarkDuplicate;

internal sealed class MarkDuplicateCommandHandler : IRequestHandler<MarkDuplicateCommand, Result>
{
    private readonly IQaReviewRepository _reviewRepository;
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IBugReportRepository _bugReportRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;
    private readonly ITrustReportRepository _trustReportRepository;

    public MarkDuplicateCommandHandler(
        IQaReviewRepository reviewRepository,
        IAnalysisRunRepository analysisRunRepository,
        IBugReportRepository bugReportRepository,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter auditWriter,
        ITrustReportRepository trustReportRepository)
    {
        _reviewRepository = reviewRepository;
        _analysisRunRepository = analysisRunRepository;
        _bugReportRepository = bugReportRepository;
        _idempotencyStore = idempotencyStore;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
        _trustReportRepository = trustReportRepository;
    }

    public async Task<Result> Handle(MarkDuplicateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        string requestHash = QaWorkflowIdempotency.Hash(
            request.AnalysisRunId, request.DuplicateTicketId, request.CandidateSnapshotHash, request.ExpectedVersion, request.Notes);
        string scopedKey = QaWorkflowIdempotency.Scope(
            _currentUser.UserId,
            "POST",
            $"/api/v1/analyses/{request.AnalysisRunId}/decisions/duplicate",
            request.IdempotencyKey);
        var idempotency = await QaWorkflowIdempotency.ReserveAsync(
            _idempotencyStore, _clock, scopedKey, requestHash, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result.Failure(idempotency.Error);
        }

        if (idempotency.Value.IsReplay)
        {
            return Result.Success();
        }

        var review = await _reviewRepository.GetByAnalysisRunIdAsync(new Domain.Analysis.AnalysisRunId(request.AnalysisRunId), cancellationToken);
        if (review == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("DUPLICATE_GATE_REQUIRED", "QA Review not found."));
        }

        TrustReport? trustReport = null;
        var latestRevision = review.Revisions.OrderByDescending(r => r.RevisionNumber).FirstOrDefault();
        if (latestRevision != null)
        {
            trustReport = await _trustReportRepository.GetLatestForTargetAsync(
                latestRevision.Id.Value,
                TrustTargetType.ReproRevision,
                cancellationToken);
        }
        else
        {
            var reproCase = await _analysisRunRepository.GetReproCaseAsync(review.AnalysisRunId, cancellationToken);
            if (reproCase != null)
            {
                trustReport = await _trustReportRepository.GetLatestForTargetAsync(
                    reproCase.Id,
                    TrustTargetType.ReproCase,
                    cancellationToken);
            }
        }

        if (trustReport == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("TRUST_REPORT_NOT_FOUND", "Trust report not found for the current review target."));
        }

        if (!trustReport.AllowedActions.Contains(AllowedQaAction.MarkDuplicate))
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("TRUST_GATE_VIOLATION", $"Action MarkDuplicate is not allowed based on the trust report outcome: {trustReport.Outcome}"));
        }
        


        if (review == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("DUPLICATE_GATE_REQUIRED", "QA Review not found."));
        }

        // Validate that DuplicateTicketId was actually part of the persisted candidates for this analysis run
        var matches = await _analysisRunRepository.GetDuplicateMatchesAsync(review.AnalysisRunId, cancellationToken);
        if (matches.All(m => m.HistoricalTicketId != request.DuplicateTicketId))
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("DUPLICATE_GATE_REQUIRED", "Selected ticket was not part of the duplicate candidates."));
        }

        var markResult = review.MarkDuplicate(
            request.DuplicateTicketId,
            request.CandidateSnapshotHash,
            request.ExpectedVersion,
            _currentUser.UserId!,
            _clock.UtcNow,
            request.Notes);

        if (markResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return markResult;
        }

        // Update BugReport Status
        var analysisRun = await _analysisRunRepository.GetAsync(review.AnalysisRunId, cancellationToken);
        var report = await _bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(analysisRun!.ReportId.Value), cancellationToken);
        
        var reportUpdateResult = report!.CloseAsDuplicate(_clock.UtcNow);
        if (reportUpdateResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return reportUpdateResult;
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await QaWorkflowIdempotency.CompleteAsync(
            _idempotencyStore, idempotency.Value, review.Id.Value, _clock, cancellationToken);

        await _auditWriter.WriteAsync(
            "QaReview",
            review.Id.Value,
            "MarkedDuplicate",
            _currentUser.UserId!,
            new { ExpectedVersion = request.ExpectedVersion, DuplicateTicketId = request.DuplicateTicketId },
            cancellationToken);

        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result.Success();
    }

    private async Task ReleaseReservationAsync(QaIdempotencyReservation reservation, CancellationToken cancellationToken)
    {
        _unitOfWork.ClearChanges();
        await QaWorkflowIdempotency.ReleaseAsync(_idempotencyStore, reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
