namespace GameBug.Api.Configuration;

public sealed class IntakeOptions
{
    public const string SectionName = "Intake";
    public long MaxRequestBodyBytes { get; init; } = 30 * 1024 * 1024;
    public int MaxFilesPerReport { get; init; } = 5;
}
