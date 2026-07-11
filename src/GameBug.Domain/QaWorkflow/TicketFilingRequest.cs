namespace GameBug.Domain.QaWorkflow;

public record TicketFilingRequestId(Guid Value)
{
    public static TicketFilingRequestId CreateUnique() => new(Guid.NewGuid());
}

public class TicketFilingRequest
{
    private TicketFilingRequest() { }

    internal TicketFilingRequest(
        TicketFilingRequestId id,
        QaReviewId reviewId,
        string idempotencyKey,
        string payloadHash,
        DateTimeOffset requestedAt)
    {
        Id = id;
        ReviewId = reviewId;
        IdempotencyKey = idempotencyKey;
        PayloadHash = payloadHash;
        RequestedAt = requestedAt;
    }

    public TicketFilingRequestId Id { get; private set; } = null!;
    public QaReviewId ReviewId { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string PayloadHash { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
}
