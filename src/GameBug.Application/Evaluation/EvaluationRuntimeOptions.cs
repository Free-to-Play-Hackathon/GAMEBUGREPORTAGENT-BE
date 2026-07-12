namespace GameBug.Application.Evaluation;

public sealed class EvaluationRuntimeOptions
{
    public string? SchemaVersion { get; set; }
    public string? SanitizerVersion { get; set; }
    public string? ParserVersion { get; set; }
    public string? RoutingPolicyVersion { get; set; }
    public string? EmbeddingVersion { get; set; }
    public string? RankerVersion { get; set; }
    public string? TrustPolicyVersion { get; set; }
    public string? SourceCommit { get; set; }
    public string? BuildVersion { get; set; }
    public int PerCaseTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Assumed manual (unassisted) triage duration per report, used as the baseline for
    /// TriageTimeSavedRate until a real paired manual-vs-assisted study is run. Not a measured value.
    /// </summary>
    public int ManualTriageBaselineSeconds { get; set; } = 900;
}
