namespace GameBug.Infrastructure.HistoricalTickets;

public sealed class HistoricalTicketIndexJobEntity
{
    private HistoricalTicketIndexJobEntity() { }

    public HistoricalTicketIndexJobEntity(Guid id, Guid ticketId, DateTimeOffset availableAt, DateTimeOffset createdAt)
    {
        Id = id;
        TicketId = ticketId;
        Status = "Queued";
        AvailableAt = availableAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Status { get; private set; } = null!;
    public int AttemptCount { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastErrorCode { get; private set; }

    public void Claim(string workerId, DateTimeOffset lockedUntil)
    {
        Status = "Processing";
        AttemptCount++;
        LockedBy = workerId;
        LockedUntil = lockedUntil;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Status = "Completed";
        CompletedAt = completedAt;
        LockedBy = null;
        LockedUntil = null;
        LastErrorCode = null;
    }

    public void Retry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        Status = "Queued";
        AvailableAt = nextAttemptAt;
        LockedBy = null;
        LockedUntil = null;
        LastErrorCode = errorCode;
    }

    public void DeadLetter(string errorCode, DateTimeOffset completedAt)
    {
        Status = "Failed";
        CompletedAt = completedAt;
        LockedBy = null;
        LockedUntil = null;
        LastErrorCode = errorCode;
    }
}
