namespace GameBug.Domain.Analysis;

public sealed class AnalysisOutboxMessage
{
    private AnalysisOutboxMessage() { }

    public AnalysisOutboxMessage(
        Guid id,
        string messageType,
        AnalysisRunId aggregateId,
        string payloadJson,
        DateTimeOffset occurredAt,
        DateTimeOffset nextAttemptAt)
    {
        Id = id;
        MessageType = messageType;
        AggregateId = aggregateId;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        DispatchStatus = "Pending";
        AttemptCount = 0;
        NextAttemptAt = nextAttemptAt;
    }

    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = null!;
    public AnalysisRunId AggregateId { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string DispatchStatus { get; private set; } = null!;
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public string? LastErrorCode { get; private set; }

    public void Claim(string workerId, DateTimeOffset lockedUntil)
    {
        DispatchStatus = "Dispatching";
        LockedBy = workerId;
        LockedUntil = lockedUntil;
    }

    public void MarkDispatched(DateTimeOffset dispatchedAt)
    {
        DispatchStatus = "Dispatched";
        DispatchedAt = dispatchedAt;
        LockedBy = null;
        LockedUntil = null;
        LastErrorCode = null;
    }

    public void MarkFailed(string errorCode, DateTimeOffset nextAttemptAt)
    {
        DispatchStatus = "Pending";
        AttemptCount++;
        NextAttemptAt = nextAttemptAt;
        LockedBy = null;
        LockedUntil = null;
        LastErrorCode = errorCode;
    }
}
