using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Evaluation.GetEvaluation;

public sealed record EvaluationRunResult(
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
    EvaluationComponentVersionResult ComponentVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyCollection<EvaluationMetricResult> Metrics,
    IReadOnlyCollection<EvaluationCaseResultDto> Cases);

public sealed record EvaluationComponentVersionResult(
    string? SchemaVersion,
    string? SanitizerVersion,
    string? ParserVersion,
    string? RoutingPolicyVersion,
    string? EmbeddingVersion,
    string? RankerVersion,
    string? TrustPolicyVersion,
    string? SourceCommit,
    string? BuildVersion);

public sealed record EvaluationMetricResult(
    string Name,
    int Numerator,
    int Denominator,
    double? Value,
    string Unit,
    string Validity);

public sealed record EvaluationCaseResultDto(
    string CaseId,
    string Outcome,
    Guid? AnalysisRunId,
    string? ExpectedDuplicateKey,
    string? ActualTopKey,
    int? ActualRank,
    string? ActualClassification,
    long? LatencyMs,
    string? ErrorCode,
    DateTimeOffset CreatedAt);

public sealed record GetEvaluationQuery(Guid RunId) : IRequest<Result<EvaluationRunResult>>;
