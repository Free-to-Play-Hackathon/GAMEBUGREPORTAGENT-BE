namespace GameBug.Application.Abstractions.AI;

public interface IEmbeddingProvider
{
    Task<EmbeddingResult> EmbedAsync(string normalizedText, CancellationToken cancellationToken);
}

public sealed record EmbeddingResult(
    float[] Vector,
    string Provider,
    string Model,
    string Version,
    int Dimension);
