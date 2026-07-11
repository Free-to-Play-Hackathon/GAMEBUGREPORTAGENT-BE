namespace GameBug.Application.Duplicates;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string Provider { get; set; } = "DeterministicHash";
    public string Model { get; set; } = "hash-embedding";
    public string Version { get; set; } = "embedding-v1";
    public int Dimension { get; set; } = 64;
    public int BatchSize { get; set; } = 16;
    public int TimeoutSeconds { get; set; } = 20;
}
