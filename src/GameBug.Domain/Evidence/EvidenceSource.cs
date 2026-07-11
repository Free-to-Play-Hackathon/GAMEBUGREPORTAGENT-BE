namespace GameBug.Domain.Evidence;

public enum EvidenceSourceType
{
    PlayerReport,
    Log,
    Metadata,
    GameCatalog,
    Screenshot
}

public enum TrustLevel
{
    UserStructured,
    Observed,
    Machine,
    Inferred
}

public class EvidenceSource
{
    // For EF Core
    private EvidenceSource() { }

    public EvidenceSource(
        EvidenceSourceType sourceType,
        string sourceRef,
        int? lineStart,
        int? lineEnd,
        string sanitizedExcerpt,
        string excerptHash,
        TrustLevel trustLevel)
    {
        SourceType = sourceType;
        SourceRef = sourceRef;
        LineStart = lineStart;
        LineEnd = lineEnd;
        SanitizedExcerpt = sanitizedExcerpt;
        ExcerptHash = excerptHash;
        TrustLevel = trustLevel;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public EvidenceSourceType SourceType { get; private set; }
    public string SourceRef { get; private set; } = null!;
    public int? LineStart { get; private set; }
    public int? LineEnd { get; private set; }
    public string SanitizedExcerpt { get; private set; } = null!;
    public string ExcerptHash { get; private set; } = null!;
    public TrustLevel TrustLevel { get; private set; }
}
