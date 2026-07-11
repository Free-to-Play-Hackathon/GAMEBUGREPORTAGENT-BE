namespace GameBug.Domain.Analysis;

public sealed class AnalysisCheckpoint
{
    private AnalysisCheckpoint() { }

    public AnalysisCheckpoint(
        Guid id,
        AnalysisRunId analysisRunId,
        AnalysisStage stage,
        string stageVersion,
        string inputHash,
        string status,
        int attempt,
        DateTimeOffset startedAt)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        Stage = stage;
        StageVersion = stageVersion;
        InputHash = inputHash;
        Status = status;
        Attempt = attempt;
        StartedAt = startedAt;
    }

    public Guid Id { get; private set; }
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public AnalysisStage Stage { get; private set; }
    public string StageVersion { get; private set; } = null!;
    public string InputHash { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public string? OutputReference { get; private set; }
    public int Attempt { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? WarningCodesJson { get; private set; }
    public string? ErrorCode { get; private set; }

    public void Complete(string outputReference, string warningCodesJson, DateTimeOffset completedAt)
    {
        Status = "Completed";
        OutputReference = outputReference;
        WarningCodesJson = warningCodesJson;
        CompletedAt = completedAt;
        ErrorCode = null;
    }

    public void Skip(string outputReference, DateTimeOffset completedAt)
    {
        Status = "Skipped";
        OutputReference = outputReference;
        CompletedAt = completedAt;
        ErrorCode = null;
    }

    public void Fail(string errorCode, DateTimeOffset completedAt)
    {
        Status = "Failed";
        ErrorCode = errorCode;
        CompletedAt = completedAt;
    }
}
