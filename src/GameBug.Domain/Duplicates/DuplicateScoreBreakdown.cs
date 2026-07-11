namespace GameBug.Domain.Duplicates;

public sealed record DuplicateScoreBreakdown(
    double? StackSignature,
    double? SemanticText,
    double? TriggerAction,
    double? SceneOrFeature,
    double? ActualResult,
    double? BuildPlatform,
    double? ScreenshotContext);
