using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Evidence;

public class EvidenceFact
{
    private readonly List<EvidenceSource> _sources = new();

    // For EF Core
    private EvidenceFact() { }

    private EvidenceFact(
        Guid id,
        string factType,
        string? normalizedValue,
        EvidenceStatus status,
        double confidence,
        IEnumerable<EvidenceSource> sources)
    {
        Id = id;
        FactType = factType;
        NormalizedValue = normalizedValue;
        Status = status;
        Confidence = confidence;
        _sources.AddRange(sources);
    }

    public Guid Id { get; private set; }
    public string FactType { get; private set; } = null!;
    public string? NormalizedValue { get; private set; }
    public EvidenceStatus Status { get; private set; }
    public double Confidence { get; private set; }

    public IReadOnlyCollection<EvidenceSource> Sources => _sources.AsReadOnly();

    public static Result<EvidenceFact> Create(
        Guid id,
        string factType,
        string? normalizedValue,
        EvidenceStatus status,
        double confidence,
        IEnumerable<EvidenceSource> sources)
    {
        if (string.IsNullOrWhiteSpace(factType))
        {
            return Result.Failure<EvidenceFact>(new DomainError("EvidenceFact.FactTypeRequired", "Fact type is required."));
        }

        if (confidence < 0.0 || confidence > 1.0)
        {
            return Result.Failure<EvidenceFact>(new DomainError("EvidenceFact.InvalidConfidence", "Confidence must be between 0.0 and 1.0."));
        }

        var sourceList = sources.ToList();

        if (status == EvidenceStatus.Supported && sourceList.Count < 1)
        {
            return Result.Failure<EvidenceFact>(new DomainError(
                "EvidenceFact.SupportedNeedsSource",
                "Supported facts must have at least one source."));
        }

        if (status == EvidenceStatus.Corroborated && sourceList.Select(source => source.SourceRef).Distinct(StringComparer.Ordinal).Count() < 2)
        {
            return Result.Failure<EvidenceFact>(new DomainError(
                "EvidenceFact.CorroboratedNeedsMultipleSources",
                "Corroborated facts must have at least two sources."));
        }


        if (status == EvidenceStatus.Conflict && sourceList.Select(source => source.SourceRef).Distinct(StringComparer.Ordinal).Count() < 2)
        {
            return Result.Failure<EvidenceFact>(new DomainError(
                "EvidenceFact.ConflictNeedsMultipleSources",
                "Conflicting facts must contain at least two independent sources."));
        }

        if (status == EvidenceStatus.Unknown && normalizedValue is not null)
        {
            return Result.Failure<EvidenceFact>(new DomainError(
                "EvidenceFact.UnknownCannotHaveValue",
                "Unknown facts cannot contain normalized value."));
        }

        return new EvidenceFact(id, factType, normalizedValue, status, confidence, sourceList);
    }
}
