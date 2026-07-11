namespace GameBug.Application.Vision;

public sealed class VisionOptions
{
    public const string SectionName = "Vision";

    public bool Enabled { get; set; } = false;
    public bool Required { get; set; } = false;
    public string Provider { get; set; } = "Disabled";
    public string Model { get; set; } = "gpt-4.1";
    public string StageVersion { get; set; } = "vision-safe-off-v1";
    public int TimeoutSeconds { get; set; } = 20;
    public int MaxImagesPerAnalysis { get; set; } = 2;

    public static bool IsValid(VisionOptions options) =>
        options.Required == false &&
        !string.IsNullOrWhiteSpace(options.Provider) &&
        !string.IsNullOrWhiteSpace(options.Model) &&
        !string.IsNullOrWhiteSpace(options.StageVersion) &&
        options.TimeoutSeconds is > 0 and <= 120 &&
        options.MaxImagesPerAnalysis is > 0 and <= 10;
}
