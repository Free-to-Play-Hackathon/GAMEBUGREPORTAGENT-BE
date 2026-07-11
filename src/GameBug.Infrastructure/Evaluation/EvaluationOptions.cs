namespace GameBug.Infrastructure.Evaluation;

public class EvaluationOptions
{
    public const string SectionName = "Evaluation";

    public string ManifestRoot { get; set; } = "evaluation/manifests";
    public string CaseRoot { get; set; } = "evaluation/cases";
    public string GroundTruthRoot { get; set; } = "evaluation/ground-truth";
    public string ArtifactRoot { get; set; } = "evaluation/artifacts";
    public string[] AllowlistedManifests { get; set; } = ["demo-v1"];
    public int PerCaseTimeoutSeconds { get; set; } = 120;
    public string SchemaVersion { get; set; } = "analysis-result-v1";
    public string SanitizerVersion { get; set; } = "sanitizer-v1";
    public string ParserVersion { get; set; } = "parser-v1";
    public string TrustPolicyVersion { get; set; } = "trust-policy-v1";
    public int WorkerHeartbeatIntervalSeconds { get; set; } = 30;
}
