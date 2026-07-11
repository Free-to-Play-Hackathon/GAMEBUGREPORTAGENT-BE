using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Time;
using GameBug.Application.Abstractions.Trust;
using GameBug.Application.QaWorkflow;
using GameBug.Application.ReproCases;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;
using GameBug.Domain.Trust;
using GameBug.Domain.Evidence;
using MediatR;

namespace GameBug.Application.QaWorkflow.ReviseRepro;

internal sealed class ReviseReproCommandHandler : IRequestHandler<ReviseReproCommand, Result>
{
    private readonly IQaReviewRepository _reviewRepository;
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IBugReportRepository _bugReportRepository;
    private readonly IReproValidator _reproValidator;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;
    private readonly IProvenanceValidator _provenanceValidator;
    private readonly IQualityGate _qualityGate;
    private readonly ITrustReportRepository _trustReportRepository;

    public ReviseReproCommandHandler(
        IQaReviewRepository reviewRepository,
        IAnalysisRunRepository analysisRunRepository,
        IBugReportRepository bugReportRepository,
        IReproValidator reproValidator,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter auditWriter,
        IProvenanceValidator provenanceValidator,
        IQualityGate qualityGate,
        ITrustReportRepository trustReportRepository)
    {
        _reviewRepository = reviewRepository;
        _analysisRunRepository = analysisRunRepository;
        _bugReportRepository = bugReportRepository;
        _reproValidator = reproValidator;
        _idempotencyStore = idempotencyStore;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
        _provenanceValidator = provenanceValidator;
        _qualityGate = qualityGate;
        _trustReportRepository = trustReportRepository;
    }

    public async Task<Result> Handle(ReviseReproCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        string requestHash = QaWorkflowIdempotency.Hash(
            request.AnalysisRunId, request.BaseReproId, request.SerializedRepro, request.ExpectedVersion);
        string scopedKey = QaWorkflowIdempotency.Scope(
            _currentUser.UserId,
            "PUT",
            $"/api/v1/analyses/{request.AnalysisRunId}/repro-case",
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

        var analysisRunId = new AnalysisRunId(request.AnalysisRunId);
        var review = await _reviewRepository.GetByAnalysisRunIdAsync(analysisRunId, cancellationToken);

        if (review == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("DUPLICATE_GATE_REQUIRED", "QA Review not found."));
        }

        var analysisRun = await _analysisRunRepository.GetAsync(analysisRunId, cancellationToken);
        if (analysisRun is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("ReviseRepro.AnalysisNotFound", "Analysis run not found."));
        }

        var report = await _bugReportRepository.GetAsync(new BugReportId(analysisRun.ReportId.Value), cancellationToken);
        if (report is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("ReviseRepro.ReportNotFound", "Bug report not found."));
        }

        var evidencePack = await _analysisRunRepository.GetEvidencePackAsync(analysisRunId, cancellationToken);
        var validated = _reproValidator.ValidateAndConstruct(
            analysisRunId,
            request.SerializedRepro,
            evidencePack?.Facts.ToList() ?? new List<Domain.Evidence.EvidenceFact>(),
            report.Description);
        if (validated.ReproCaseResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(validated.ReproCaseResult.Error);
        }

        var generatedRepro = await _analysisRunRepository.GetReproCaseAsync(analysisRunId, cancellationToken);
        Guid? baseReproId = request.BaseReproId ?? generatedRepro?.Id;
        if (baseReproId is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("ReviseRepro.BaseReproRequired", "A generated repro case is required for the first revision."));
        }

        var addRevisionResult = review.AddRevision(
            baseReproId,
            request.SerializedRepro,
            request.ExpectedVersion,
            _currentUser.UserId!,
            _clock.UtcNow);

        if (addRevisionResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return addRevisionResult;
        }

        var latestRevision = review.Revisions.OrderByDescending(r => r.RevisionNumber).First();
        var evPack = evidencePack ?? new EvidencePack(Guid.NewGuid(), analysisRunId, new List<EvidenceFact>(), new List<EventTimelineEntry>());

        var provenanceViolations = _provenanceValidator.Validate(analysisRunId, evPack, validated.ReproCaseResult.Value, validated.Warnings);
        var trustReportResult = _qualityGate.Evaluate(
            analysisRunId,
            latestRevision.Id.Value,
            TrustTargetType.ReproRevision,
            provenanceViolations,
            evPack,
            validated.ReproCaseResult.Value,
            duplicateSearchComplete: true,
            requestHash);

        if (trustReportResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(trustReportResult.Error);
        }

        await _trustReportRepository.AddAsync(trustReportResult.Value, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await QaWorkflowIdempotency.CompleteAsync(
            _idempotencyStore, idempotency.Value, review.Id.Value, _clock, cancellationToken);

        await _auditWriter.WriteAsync(
            "QaReview",
            review.Id.Value,
            "ReproRevised",
            _currentUser.UserId!,
            new { BaseReproId = request.BaseReproId },
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
