using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.QaWorkflow;

public class QaReview
{
    private readonly List<ReproRevision> _revisions = new();
    private readonly List<ClarificationRequest> _clarificationRequests = new();

    private QaReview() { }

    private QaReview(
        QaReviewId id,
        AnalysisRunId analysisRunId,
        string candidateSnapshotHash,
        string openedBy,
        DateTimeOffset openedAt)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        CandidateSnapshotHash = candidateSnapshotHash;
        Status = QaReviewStatus.Open;
        Version = 1;
        OpenedBy = openedBy;
        OpenedAt = openedAt;
    }

    public QaReviewId Id { get; private set; } = null!;
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public string CandidateSnapshotHash { get; private set; } = null!;
    public QaReviewStatus Status { get; private set; }
    public int Version { get; private set; }
    public string OpenedBy { get; private set; } = null!;
    public DateTimeOffset OpenedAt { get; private set; }
    public QaDecision? Decision { get; private set; }
    public InternalTicket? InternalTicket { get; private set; }
    public uint VersionToken { get; private set; } // For optimistic concurrency if needed at repo layer, but Version is used for business logic

    public IReadOnlyCollection<ReproRevision> Revisions => _revisions.AsReadOnly();
    public IReadOnlyCollection<ClarificationRequest> ClarificationRequests => _clarificationRequests.AsReadOnly();

    public static Result<QaReview> Open(
        QaReviewId id,
        AnalysisRun analysis,
        string candidateSnapshotHash,
        string openedBy,
        DateTimeOffset openedAt)
    {
        if (analysis.Status != AnalysisStatus.AwaitingQaReview)
        {
            return Result.Failure<QaReview>(new DomainError("QaReview.InvalidAnalysisStatus", "Analysis must be in AwaitingQaReview status to open a review."));
        }

        if (string.IsNullOrWhiteSpace(candidateSnapshotHash))
        {
            return Result.Failure<QaReview>(new DomainError("QaReview.SnapshotHashRequired", "Candidate snapshot hash cannot be empty."));
        }

        return new QaReview(id, analysis.Id, candidateSnapshotHash, openedBy, openedAt);
    }

    public Result AddRevision(Guid? baseReproId, string serializedRepro, int expectedVersion, string editor, DateTimeOffset editedAt)
    {
        if (Version != expectedVersion)
            return Result.Failure(new DomainError("QA_REVIEW_VERSION_CONFLICT", "QA Review version conflict."));

        if (Status != QaReviewStatus.Open && Status != QaReviewStatus.MoreInformationRequested)
        {
            return Result.Failure(new DomainError("QaReview.NotOpen", "Cannot add revision unless review is Open or MoreInformationRequested."));
        }

        var parentRevision = _revisions.OrderByDescending(r => r.RevisionNumber).FirstOrDefault();
        int newRevisionNumber = (parentRevision?.RevisionNumber ?? 0) + 1;

        var revision = new ReproRevision(
            ReproRevisionId.CreateUnique(),
            Id,
            newRevisionNumber,
            baseReproId,
            parentRevision?.Id,
            serializedRepro,
            editor,
            editedAt);

        _revisions.Add(revision);
        Version++;
        VersionToken++;

        return Result.Success();
    }

    public Result AcknowledgeSnapshot(string candidateSnapshotHash)
    {
        if (CandidateSnapshotHash != candidateSnapshotHash)
        {
            return Result.Failure(new DomainError("CANDIDATE_SNAPSHOT_MISMATCH", "Provided snapshot hash does not match the bound snapshot."));
        }
        return Result.Success();
    }

    public Result MarkDuplicate(
        Guid duplicateTicketId,
        string candidateSnapshotHash,
        int expectedVersion,
        string actor,
        DateTimeOffset decidedAt,
        string? notes)
    {
        if (Version != expectedVersion)
            return Result.Failure(new DomainError("QA_REVIEW_VERSION_CONFLICT", "QA Review version conflict."));

        if (Status != QaReviewStatus.Open)
            return Result.Failure(new DomainError("QA_DECISION_ALREADY_FINAL", "Review is already finalized or not open."));

        var ackResult = AcknowledgeSnapshot(candidateSnapshotHash);
        if (ackResult.IsFailure) return ackResult;

        Decision = QaDecision.CreateDuplicate(Id, duplicateTicketId, actor, decidedAt, notes);
        Status = QaReviewStatus.DuplicateMarked;
        Version++;
        VersionToken++;

        return Result.Success();
    }

    public Result CreateNewTicket(
        string candidateSnapshotHash,
        int expectedVersion,
        string actor,
        DateTimeOffset decidedAt,
        string? notes)
    {
        if (Version != expectedVersion)
            return Result.Failure(new DomainError("QA_REVIEW_VERSION_CONFLICT", "QA Review version conflict."));

        if (Status != QaReviewStatus.Open)
            return Result.Failure(new DomainError("QA_DECISION_ALREADY_FINAL", "Review is already finalized or not open."));

        var ackResult = AcknowledgeSnapshot(candidateSnapshotHash);
        if (ackResult.IsFailure) return ackResult;

        Decision = QaDecision.CreateNew(Id, actor, decidedAt, notes);
        Status = QaReviewStatus.NewTicketCreated;
        Version++;
        VersionToken++;

        return Result.Success();
    }

    public Result RejectAnalysis(
        string reasonCode,
        string? notes,
        int expectedVersion,
        string actor,
        DateTimeOffset decidedAt)
    {
        if (Version != expectedVersion)
            return Result.Failure(new DomainError("QA_REVIEW_VERSION_CONFLICT", "QA Review version conflict."));

        if (Status != QaReviewStatus.Open)
            return Result.Failure(new DomainError("QA_DECISION_ALREADY_FINAL", "Review is already finalized or not open."));

        if (string.IsNullOrWhiteSpace(reasonCode))
            return Result.Failure(new DomainError("QaReview.RejectReasonRequired", "Reject reason code is required."));

        Decision = QaDecision.CreateReject(Id, reasonCode, actor, decidedAt, notes);
        Status = QaReviewStatus.Rejected;
        Version++;
        VersionToken++;

        return Result.Success();
    }

    public Result<ClarificationRequest> RequestMoreInformation(
        List<string> questions,
        int expectedVersion,
        string requester,
        DateTimeOffset requestedAt)
    {
        if (Version != expectedVersion)
            return Result.Failure<ClarificationRequest>(new DomainError("QA_REVIEW_VERSION_CONFLICT", "QA Review version conflict."));

        if (Status != QaReviewStatus.Open)
            return Result.Failure<ClarificationRequest>(new DomainError("QaReview.NotOpen", "Review is not open."));

        if (questions.Count == 0 || questions.Count > 3)
            return Result.Failure<ClarificationRequest>(new DomainError("QaReview.InvalidQuestionCount", "Must provide 1 to 3 questions."));

        var request = new ClarificationRequest(ClarificationRequestId.CreateUnique(), Id, requester, requestedAt);
        foreach (var q in questions)
        {
            var trimmed = q.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return Result.Failure<ClarificationRequest>(new DomainError("QaReview.QuestionRequired", "Question text is required."));

            if (trimmed.Length > 500)
                return Result.Failure<ClarificationRequest>(new DomainError("QaReview.QuestionTooLong", "Question is too long (max 500 chars)."));
            
            request.AddQuestion(new ClarificationQuestion(ClarificationQuestionId.CreateUnique(), request.Id, trimmed));
        }

        _clarificationRequests.Add(request);
        Status = QaReviewStatus.MoreInformationRequested;
        Version++;
        VersionToken++;

        return request;
    }

    public Result AttachInternalTicket(string externalId, string systemName, string url, DateTimeOffset filedAt)
    {
        if (Status != QaReviewStatus.NewTicketCreated)
            return Result.Failure(new DomainError("QaReview.InvalidStatus", "Cannot attach ticket unless status is NewTicketCreated."));

        if (InternalTicket != null)
            return Result.Failure(new DomainError("QaReview.TicketAlreadyAttached", "An internal ticket is already attached to this review."));

        InternalTicket = new InternalTicket(InternalTicketId.CreateUnique(), Id, externalId, systemName, url, filedAt);
        VersionToken++;

        return Result.Success();
    }
}
