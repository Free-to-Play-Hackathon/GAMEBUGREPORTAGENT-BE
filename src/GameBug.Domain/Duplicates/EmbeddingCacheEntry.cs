namespace GameBug.Domain.Duplicates;

public class EmbeddingCacheEntry
{
    private EmbeddingCacheEntry() { }

    public EmbeddingCacheEntry(
        Guid id,
        string contentHash,
        string provider,
        string model,
        string embeddingVersion,
        float[] vector,
        int dimension,
        DateTimeOffset createdAt)
    {
        Id = id;
        ContentHash = contentHash;
        Provider = provider;
        Model = model;
        EmbeddingVersion = embeddingVersion;
        Vector = vector;
        Dimension = dimension;
        CreatedAt = createdAt;
        LastUsedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public string EmbeddingVersion { get; private set; } = null!;
    public float[] Vector { get; private set; } = Array.Empty<float>();
    public int Dimension { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }

    public void MarkUsed(DateTimeOffset usedAt) => LastUsedAt = usedAt;
}
