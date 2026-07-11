using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Duplicates;

public class HistoricalTicket
{
    private HistoricalTicket() { }

    private HistoricalTicket(
        Guid id,
        Guid projectId,
        string source,
        string externalId,
        string title,
        string summarySanitized,
        string status,
        string severity,
        string? buildMin,
        string? buildMax,
        string[] platforms,
        string? stackSignature,
        string? stackSummary,
        string[] gameEntities,
        string? symptom,
        string? triggerAction,
        string? sceneOrFeature,
        string? actualResult,
        string searchText,
        string searchTextHash,
        string importVersion,
        DateTimeOffset sourceUpdatedAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        Source = source.Trim();
        ExternalId = externalId.Trim();
        Title = title.Trim();
        SummarySanitized = summarySanitized.Trim();
        Status = Normalize(status, "unknown");
        Severity = Normalize(severity, "unknown");
        BuildMin = string.IsNullOrWhiteSpace(buildMin) ? null : buildMin.Trim();
        BuildMax = string.IsNullOrWhiteSpace(buildMax) ? null : buildMax.Trim();
        Platforms = platforms.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        StackSignature = string.IsNullOrWhiteSpace(stackSignature) ? null : stackSignature.Trim();
        StackSummary = string.IsNullOrWhiteSpace(stackSummary) ? null : stackSummary.Trim();
        GameEntities = gameEntities.Select(NormalizeToken).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Symptom = string.IsNullOrWhiteSpace(symptom) ? null : symptom.Trim();
        TriggerAction = string.IsNullOrWhiteSpace(triggerAction) ? null : triggerAction.Trim();
        SceneOrFeature = string.IsNullOrWhiteSpace(sceneOrFeature) ? null : sceneOrFeature.Trim();
        ActualResult = string.IsNullOrWhiteSpace(actualResult) ? null : actualResult.Trim();
        SearchText = searchText.Trim();
        SearchTextHash = searchTextHash;
        ImportVersion = importVersion.Trim();
        SourceUpdatedAt = sourceUpdatedAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Source { get; private set; } = null!;
    public string ExternalId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string SummarySanitized { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public string Severity { get; private set; } = null!;
    public string? BuildMin { get; private set; }
    public string? BuildMax { get; private set; }
    public string[] Platforms { get; private set; } = Array.Empty<string>();
    public string? StackSignature { get; private set; }
    public string? StackSummary { get; private set; }
    public string[] GameEntities { get; private set; } = Array.Empty<string>();
    public string? Symptom { get; private set; }
    public string? TriggerAction { get; private set; }
    public string? SceneOrFeature { get; private set; }
    public string? ActualResult { get; private set; }
    public string SearchText { get; private set; } = null!;
    public string SearchTextHash { get; private set; } = null!;
    public float[]? Embedding { get; private set; }
    public string? EmbeddingProvider { get; private set; }
    public string? EmbeddingModel { get; private set; }
    public string? EmbeddingVersion { get; private set; }
    public int? EmbeddingDimension { get; private set; }
    public DateTimeOffset SourceUpdatedAt { get; private set; }
    public DateTimeOffset? IndexedAt { get; private set; }
    public string ImportVersion { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<HistoricalTicket> Create(
        Guid id,
        Guid projectId,
        string source,
        string externalId,
        string title,
        string summarySanitized,
        string status,
        string severity,
        string? buildMin,
        string? buildMax,
        IEnumerable<string> platforms,
        string? stackSignature,
        string? stackSummary,
        IEnumerable<string> gameEntities,
        string? symptom,
        string? triggerAction,
        string? sceneOrFeature,
        string? actualResult,
        string searchText,
        string searchTextHash,
        string importVersion,
        DateTimeOffset sourceUpdatedAt,
        DateTimeOffset createdAt)
    {
        if (projectId == Guid.Empty)
        {
            return Result.Failure<HistoricalTicket>(new DomainError("HistoricalTicket.ProjectRequired", "Project id is required."));
        }

        if (string.IsNullOrWhiteSpace(source) || source.Length > 50)
        {
            return Result.Failure<HistoricalTicket>(new DomainError("HistoricalTicket.InvalidSource", "Source is required and must be at most 50 characters."));
        }

        if (string.IsNullOrWhiteSpace(externalId) || externalId.Length > 100)
        {
            return Result.Failure<HistoricalTicket>(new DomainError("HistoricalTicket.InvalidExternalId", "External id is required and must be at most 100 characters."));
        }

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summarySanitized))
        {
            return Result.Failure<HistoricalTicket>(new DomainError("HistoricalTicket.ContentRequired", "Title and sanitized summary are required."));
        }

        if (string.IsNullOrWhiteSpace(searchText) || string.IsNullOrWhiteSpace(searchTextHash))
        {
            return Result.Failure<HistoricalTicket>(new DomainError("HistoricalTicket.SearchDocumentRequired", "Search text and hash are required."));
        }

        return new HistoricalTicket(
            id,
            projectId,
            source,
            externalId,
            title,
            summarySanitized,
            status,
            severity,
            buildMin,
            buildMax,
            platforms.ToArray(),
            stackSignature,
            stackSummary,
            gameEntities.ToArray(),
            symptom,
            triggerAction,
            sceneOrFeature,
            actualResult,
            searchText,
            searchTextHash,
            importVersion,
            sourceUpdatedAt,
            createdAt);
    }

    public void UpdateFromImport(HistoricalTicket imported, DateTimeOffset updatedAt)
    {
        Title = imported.Title;
        SummarySanitized = imported.SummarySanitized;
        Status = imported.Status;
        Severity = imported.Severity;
        BuildMin = imported.BuildMin;
        BuildMax = imported.BuildMax;
        Platforms = imported.Platforms;
        StackSignature = imported.StackSignature;
        StackSummary = imported.StackSummary;
        GameEntities = imported.GameEntities;
        Symptom = imported.Symptom;
        TriggerAction = imported.TriggerAction;
        SceneOrFeature = imported.SceneOrFeature;
        ActualResult = imported.ActualResult;
        SearchText = imported.SearchText;
        SearchTextHash = imported.SearchTextHash;
        ImportVersion = imported.ImportVersion;
        SourceUpdatedAt = imported.SourceUpdatedAt;
        UpdatedAt = updatedAt;
    }

    public void SetEmbedding(
        float[] embedding,
        string provider,
        string model,
        string version,
        int dimension,
        DateTimeOffset indexedAt)
    {
        if (embedding.Length != dimension || embedding.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
        {
            throw new ArgumentException("Embedding must contain finite values and match the configured dimension.", nameof(embedding));
        }

        Embedding = embedding;
        EmbeddingProvider = provider;
        EmbeddingModel = model;
        EmbeddingVersion = version;
        EmbeddingDimension = dimension;
        IndexedAt = indexedAt;
        UpdatedAt = indexedAt;
    }

    private static string Normalize(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : NormalizeToken(value);

    private static string NormalizeToken(string value) => value.Trim().ToLowerInvariant();
}
