using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Time;
using GameBug.Application.QaWorkflow;
using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.QaWorkflow.RequestInformation;

internal sealed class RequestInformationCommandHandler : IRequestHandler<RequestInformationCommand, Result<Guid>>
{
    private readonly IQaReviewRepository _reviewRepository;
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IBugReportRepository _bugReportRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public RequestInformationCommandHandler(
        IQaReviewRepository reviewRepository,
        IAnalysisRunRepository analysisRunRepository,
        IBugReportRepository bugReportRepository,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _reviewRepository = reviewRepository;
        _analysisRunRepository = analysisRunRepository;
        _bugReportRepository = bugReportRepository;
        _idempotencyStore = idempotencyStore;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<Result<Guid>> Handle(RequestInformationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<Guid>(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        string requestHash = QaWorkflowIdempotency.Hash(
            request.AnalysisRunId, request.ExpectedVersion, request.Questions);
        string scopedKey = QaWorkflowIdempotency.Scope(
            _currentUser.UserId,
            "POST",
            $"/api/v1/analyses/{request.AnalysisRunId}/clarifications",
            request.IdempotencyKey);
        var idempotency = await QaWorkflowIdempotency.ReserveAsync(
            _idempotencyStore, _clock, scopedKey, requestHash, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result.Failure<Guid>(idempotency.Error);
        }

        if (idempotency.Value.IsReplay && idempotency.Value.ReplayId.HasValue)
        {
            return idempotency.Value.ReplayId.Value;
        }

        var review = await _reviewRepository.GetByAnalysisRunIdAsync(new Domain.Analysis.AnalysisRunId(request.AnalysisRunId), cancellationToken);

        if (review == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(new DomainError("DUPLICATE_GATE_REQUIRED", "QA Review not found."));
        }

        var requestResult = review.RequestMoreInformation(
            request.Questions,
            request.ExpectedVersion,
            _currentUser.UserId!,
            _clock.UtcNow);

        if (requestResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(requestResult.Error);
        }

        var clarificationRequest = requestResult.Value;

        // Update BugReport Status
        var analysisRun = await _analysisRunRepository.GetAsync(review.AnalysisRunId, cancellationToken);
        var report = await _bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(analysisRun!.ReportId.Value), cancellationToken);

        var reportUpdateResult = report!.RequestMoreInformation(_clock.UtcNow);
        if (reportUpdateResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(reportUpdateResult.Error);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await QaWorkflowIdempotency.CompleteAsync(
            _idempotencyStore, idempotency.Value, clarificationRequest.Id.Value, _clock, cancellationToken);

        await _auditWriter.WriteAsync(
            "QaReview",
            review.Id.Value,
            "MoreInformationRequested",
            _currentUser.UserId!,
            new { ExpectedVersion = request.ExpectedVersion, ClarificationRequestId = clarificationRequest.Id.Value, QuestionsCount = request.Questions.Count },
            cancellationToken);

        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return clarificationRequest.Id.Value;
    }

    private async Task ReleaseReservationAsync(QaIdempotencyReservation reservation, CancellationToken cancellationToken)
    {
        _unitOfWork.ClearChanges();
        await QaWorkflowIdempotency.ReleaseAsync(_idempotencyStore, reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
