namespace GameBug.Domain.Analysis;

public sealed class AnalysisAttempt
{
    private AnalysisAttempt() { }

    public AnalysisAttempt(
        Guid id,
        AnalysisRunId analysisRunId,
        Guid jobId,
        string workerId,
        int attemptNumber,
        DateTimeOffset startedAt)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        JobId = jobId;
        WorkerId = workerId;
        AttemptNumber = attemptNumber;
        StartedAt = startedAt;
        Outcome = "Running";
    }

    public Guid Id { get; private set; }
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public Guid JobId { get; private set; }
    public string WorkerId { get; private set; } = null!;
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string Outcome { get; private set; } = null!;
    public string? ErrorCode { get; private set; }
    public long? DurationMs { get; private set; }

    public void Finish(string outcome, string? errorCode, DateTimeOffset completedAt)
    {
        Outcome = outcome;
        ErrorCode = errorCode;
        CompletedAt = completedAt;
        DurationMs = (long)(completedAt - StartedAt).TotalMilliseconds;
    }
}
