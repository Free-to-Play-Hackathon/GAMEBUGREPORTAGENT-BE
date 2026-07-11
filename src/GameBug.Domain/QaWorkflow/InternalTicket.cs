namespace GameBug.Domain.QaWorkflow;

public record InternalTicketId(Guid Value)
{
    public static InternalTicketId CreateUnique() => new(Guid.NewGuid());
}

public class InternalTicket
{
    private InternalTicket() { }

    internal InternalTicket(
        InternalTicketId id,
        QaReviewId reviewId,
        string externalTicketId,
        string systemName,
        string url,
        DateTimeOffset filedAt)
    {
        Id = id;
        ReviewId = reviewId;
        ExternalTicketId = externalTicketId;
        SystemName = systemName;
        Url = url;
        FiledAt = filedAt;
    }

    public InternalTicketId Id { get; private set; } = null!;
    public QaReviewId ReviewId { get; private set; } = null!;
    public string ExternalTicketId { get; private set; } = null!;
    public string SystemName { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public DateTimeOffset FiledAt { get; private set; }
}
