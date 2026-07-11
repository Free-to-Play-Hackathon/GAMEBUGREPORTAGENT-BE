namespace GameBug.Infrastructure.AI.Providers;

public class GeminiOptions
{
    public const string SectionName = "Ai:Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash";
    public int TimeoutSeconds { get; set; } = 30;
}
