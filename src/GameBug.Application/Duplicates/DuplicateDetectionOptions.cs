namespace GameBug.Application.Duplicates;

public sealed class DuplicateDetectionOptions
{
    public const string SectionName = "DuplicateDetection";

    public int CandidateLimitPerChannel { get; set; } = 20;
    public int CandidatePoolLimit { get; set; } = 20;
    public int ResultLimit { get; set; } = 3;
    public int RrfConstant { get; set; } = 60;
    public string RankerVersion { get; set; } = "hybrid-v1";
    public bool EnableAiReranker { get; set; }
    public int RerankerCandidateLimit { get; set; } = 5;
    public SignalWeightOptions SignalWeights { get; set; } = new();
    public DuplicateThresholdOptions Thresholds { get; set; } = new();
}

public sealed class SignalWeightOptions
{
    public double StackSignature { get; set; } = 0.30;
    public double SemanticText { get; set; } = 0.25;
    public double SceneOrFeature { get; set; } = 0.15;
    public double TriggerAction { get; set; } = 0.10;
    public double ActualResult { get; set; } = 0.10;
    public double BuildPlatform { get; set; } = 0.10;
}

public sealed class DuplicateThresholdOptions
{
    public double LikelyDuplicate { get; set; } = 0.82;
    public double RelatedIssue { get; set; } = 0.55;
    public double InsufficientEvidenceAvailableWeight { get; set; } = 0.45;
}
