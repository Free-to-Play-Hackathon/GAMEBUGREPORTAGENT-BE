using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Time;
using GameBug.Application.Abstractions.Jobs;
using GameBug.Application.QaWorkflow;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace GameBug.Application.QaWorkflow.AnswerClarification;

internal sealed class AnswerClarificationCommandHandler : IRequestHandler<AnswerClarificationCommand, Result<Guid>>
{
    private readonly IQaReviewRepository _reviewRepository;
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IBugReportRepository _bugReportRepository;
    private readonly IAnalysisOutboxStore _outboxStore;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public AnswerClarificationCommandHandler(
        IQaReviewRepository reviewRepository,
        IAnalysisRunRepository analysisRunRepository,
        IBugReportRepository bugReportRepository,
        IAnalysisOutboxStore outboxStore,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _reviewRepository = reviewRepository;
        _analysisRunRepository = analysisRunRepository;
        _bugReportRepository = bugReportRepository;
        _outboxStore = outboxStore;
        _idempotencyStore = idempotencyStore;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<Result<Guid>> Handle(AnswerClarificationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<Guid>(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        string answerHash = string.Join('|', request.Answers
            .OrderBy(answer => answer.QuestionId)
            .Select(answer => $"{answer.QuestionId}:{answer.AnswerText.Trim()}"));
        string requestHash = QaWorkflowIdempotency.Hash(request.AnalysisRunId, request.RequestId, answerHash);
        string scopedKey = QaWorkflowIdempotency.Scope(
            _currentUser.UserId,
            "POST",
            $"/api/v1/analyses/{request.AnalysisRunId}/clarifications/{request.RequestId}/answers",
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

        var analysisRunId = new AnalysisRunId(request.AnalysisRunId);
        var review = await _reviewRepository.GetByAnalysisRunIdAsync(analysisRunId, cancellationToken);

        if (review == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(new DomainError("DUPLICATE_GATE_REQUIRED", "QA Review not found."));
        }

        var clarificationRequest = review.ClarificationRequests.FirstOrDefault(c => c.Id.Value == request.RequestId);
        if (clarificationRequest == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(new DomainError("AnswerClarification.RequestNotFound", "Clarification request not found."));
        }

        if (clarificationRequest.ResultingAnalysisRunId != null)
        {
            await QaWorkflowIdempotency.CompleteAsync(
                _idempotencyStore, idempotency.Value, clarificationRequest.ResultingAnalysisRunId.Value, _clock, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return clarificationRequest.ResultingAnalysisRunId.Value;
        }

        if (request.Answers.Count != clarificationRequest.Questions.Count)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(new DomainError("AnswerClarification.Mismatch", "Number of answers does not match the number of questions."));
        }

        foreach (var answerInput in request.Answers)
        {
            var question = clarificationRequest.Questions.FirstOrDefault(q => q.Id.Value == answerInput.QuestionId);
            if (question == null)
            {
                await ReleaseReservationAsync(idempotency.Value, cancellationToken);
                return Result.Failure<Guid>(new DomainError("AnswerClarification.QuestionNotFound", $"Question {answerInput.QuestionId} not found in request."));
            }

            question.AddAnswer(new ClarificationAnswer(
                ClarificationAnswerId.CreateUnique(),
                question.Id,
                answerInput.AnswerText,
                _currentUser.UserId!,
                _clock.UtcNow));
        }

        // Start new analysis run
        var oldAnalysisRun = await _analysisRunRepository.GetAsync(review.AnalysisRunId, cancellationToken);
        if (oldAnalysisRun is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(new DomainError("AnswerClarification.AnalysisNotFound", "Analysis run not found."));
        }

        var report = await _bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(oldAnalysisRun.ReportId.Value), cancellationToken);
        if (report is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(new DomainError("AnswerClarification.ReportNotFound", "Bug report not found."));
        }

        string? clarifiedBuildVersion = null;
        string? clarifiedPlatform = null;
        foreach (var answerInput in request.Answers)
        {
            var question = clarificationRequest.Questions.Single(q => q.Id.Value == answerInput.QuestionId);
            if (question.QuestionText.Contains("build", StringComparison.OrdinalIgnoreCase))
            {
                clarifiedBuildVersion = answerInput.AnswerText;
            }
            else if (question.QuestionText.Contains("platform", StringComparison.OrdinalIgnoreCase))
            {
                clarifiedPlatform = answerInput.AnswerText;
            }
        }

        var metadataUpdate = report.ApplyClarifiedMetadata(clarifiedBuildVersion, clarifiedPlatform, _clock.UtcNow);
        if (metadataUpdate.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(metadataUpdate.Error);
        }

        var latestRun = await _analysisRunRepository.GetLatestByReportIdAsync(report.Id, cancellationToken);

        // Recompute input hash (including new answers in the hashing context)
        int nextVersion = (latestRun?.Version ?? oldAnalysisRun.Version) + 1;
        string baseData = $"{report.Id.Value}|{nextVersion}|{report.Description}|{report.BuildVersion}|{report.Platform}";
        string clarificationContext = string.Join("|", request.Answers.Select(a => $"{a.QuestionId}:{a.AnswerText}"));
        string newInputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(baseData + "|" + clarificationContext)));

        var newRunResult = AnalysisRun.Create(
            AnalysisRunId.CreateUnique(),
            report.Id,
            nextVersion,
            newInputHash,
            oldAnalysisRun.ConfigurationHash,
            oldAnalysisRun.SchemaVersion);

        if (newRunResult.IsFailure)
        {
            return Result.Failure<Guid>(newRunResult.Error);
        }

        var newRun = newRunResult.Value;
        newRun.Queue(_clock.UtcNow); // Assuming we immediately queue it

        clarificationRequest.SetResultingAnalysis(newRun.Id);

        await _analysisRunRepository.AddAsync(newRun, cancellationToken);

        var reportUpdateResult = report.UpdateStatus(ReportStatus.Submitted, _clock.UtcNow);
        if (reportUpdateResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure<Guid>(reportUpdateResult.Error);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _auditWriter.WriteAsync(
            "QaReview",
            review.Id.Value,
            "ClarificationAnswered",
            _currentUser.UserId!,
            new { RequestId = request.RequestId, NewAnalysisRunId = newRun.Id.Value },
            cancellationToken);

        await _outboxStore.AddProcessAnalysisMessageAsync(newRun.Id, newRun.Version, cancellationToken);
        await QaWorkflowIdempotency.CompleteAsync(
            _idempotencyStore, idempotency.Value, newRun.Id.Value, _clock, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return newRun.Id.Value;
    }

    private async Task ReleaseReservationAsync(QaIdempotencyReservation reservation, CancellationToken cancellationToken)
    {
        _unitOfWork.ClearChanges();
        await QaWorkflowIdempotency.ReleaseAsync(_idempotencyStore, reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
