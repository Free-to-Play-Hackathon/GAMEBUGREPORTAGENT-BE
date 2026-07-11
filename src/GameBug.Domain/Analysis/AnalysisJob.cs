namespace GameBug.Domain.Analysis;

public sealed class AnalysisJob
{
    private AnalysisJob() { }

    public AnalysisJob(
        Guid id,
        string queueName,
        AnalysisRunId analysisRunId,
        int expectedVersion,
        DateTimeOffset availableAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        QueueName = queueName;
        AnalysisRunId = analysisRunId;
        ExpectedVersion = expectedVersion;
        Status = "Queued";
        AvailableAt = availableAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string QueueName { get; private set; } = null!;
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public int ExpectedVersion { get; private set; }
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
