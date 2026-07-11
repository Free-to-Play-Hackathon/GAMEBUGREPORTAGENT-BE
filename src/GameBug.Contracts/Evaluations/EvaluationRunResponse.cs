namespace GameBug.Contracts.Evaluations;

public sealed record EvaluationRunResponse(
    Guid RunId,
    string ManifestId,
    string ManifestHash,
    string ConfigurationHash,
    string ProtocolVersion,
    string DatasetVersion,
    string GroundTruthVersion,
    string Status,
    string Validity,
    string? InvalidReason,
    ComponentVersionResponse ComponentVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<MetricResponse> Metrics,
    IReadOnlyCollection<EvaluationCaseResponse> Cases);

public sealed record ComponentVersionResponse(
    string? SchemaVersion,
    string? SanitizerVersion,
    string? ParserVersion,
    string? RoutingPolicyVersion,
    string? EmbeddingVersion,
    string? RankerVersion,
    string? TrustPolicyVersion,
    string? SourceCommit,
    string? BuildVersion);
