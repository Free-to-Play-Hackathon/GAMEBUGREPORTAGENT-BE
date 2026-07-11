namespace GameBug.Infrastructure.AI.Providers;

public sealed class OpenAiOptions
{
    public const string SectionName = "Ai:OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public int TimeoutSeconds { get; set; } = 60;
}
