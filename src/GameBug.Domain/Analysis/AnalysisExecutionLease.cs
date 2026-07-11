namespace GameBug.Domain.Analysis;

public sealed class AnalysisExecutionLease
{
    private AnalysisExecutionLease() { }

    public AnalysisExecutionLease(AnalysisRunId analysisRunId, string lockedBy, DateTimeOffset lockedUntil)
    {
        AnalysisRunId = analysisRunId;
        LockedBy = lockedBy;
        LockedUntil = lockedUntil;
    }

    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public string LockedBy { get; private set; } = null!;
    public DateTimeOffset LockedUntil { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
