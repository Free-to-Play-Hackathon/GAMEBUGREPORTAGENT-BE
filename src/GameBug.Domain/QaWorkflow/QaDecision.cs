using GameBug.Domain.Duplicates;

namespace GameBug.Domain.QaWorkflow;

public record QaDecisionId(Guid Value)
{
    public static QaDecisionId CreateUnique() => new(Guid.NewGuid());
}

public class QaDecision
{
    private QaDecision() { }

    private QaDecision(
        QaDecisionId id,
        QaReviewId reviewId,
        QaDecisionAction action,
        string actor,
        DateTimeOffset decidedAt,
        Guid? duplicateOfTicketId,
        string? rejectReasonCode,
        string? notes)
    {
        Id = id;
        ReviewId = reviewId;
        Action = action;
        Actor = actor;
        DecidedAt = decidedAt;
        DuplicateOfTicketId = duplicateOfTicketId;
        RejectReasonCode = rejectReasonCode;
        Notes = notes;
    }

    public QaDecisionId Id { get; private set; } = null!;
    public QaReviewId ReviewId { get; private set; } = null!;
    public QaDecisionAction Action { get; private set; }
    public string Actor { get; private set; } = null!;
    public DateTimeOffset DecidedAt { get; private set; }
    public Guid? DuplicateOfTicketId { get; private set; }
    public string? RejectReasonCode { get; private set; }
    public string? Notes { get; private set; }

    internal static QaDecision CreateDuplicate(QaReviewId reviewId, Guid duplicateOfTicketId, string actor, DateTimeOffset decidedAt, string? notes)
        => new(QaDecisionId.CreateUnique(), reviewId, QaDecisionAction.MarkDuplicate, actor, decidedAt, duplicateOfTicketId, null, notes);

    internal static QaDecision CreateNew(QaReviewId reviewId, string actor, DateTimeOffset decidedAt, string? notes)
        => new(QaDecisionId.CreateUnique(), reviewId, QaDecisionAction.CreateNew, actor, decidedAt, null, null, notes);

    internal static QaDecision CreateReject(QaReviewId reviewId, string reasonCode, string actor, DateTimeOffset decidedAt, string? notes)
        => new(QaDecisionId.CreateUnique(), reviewId, QaDecisionAction.RejectAnalysis, actor, decidedAt, null, reasonCode, notes);
}
