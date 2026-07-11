using System.Text.Json;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Evaluation;

public class EvaluationRun
{
    private readonly List<EvaluationCaseResult> _caseResults = new();
    private string _metricsJson = "[]";

    private EvaluationRun() { }

    public Guid Id { get; private set; }
    public string ManifestId { get; private set; } = null!;
    public string ManifestHash { get; private set; } = null!;
    public string ConfigurationHash { get; private set; } = null!;
    public string ProtocolVersion { get; private set; } = null!;
    public string DatasetVersion { get; private set; } = null!;
    public string GroundTruthVersion { get; private set; } = null!;
    public string? SchemaVersion { get; private set; }
    public string? SanitizerVersion { get; private set; }
    public string? ParserVersion { get; private set; }
    public string? RoutingPolicyVersion { get; private set; }
    public string? EmbeddingVersion { get; private set; }
    public string? RankerVersion { get; private set; }
    public string? TrustPolicyVersion { get; private set; }
    public string? SourceCommit { get; private set; }
    public string? BuildVersion { get; private set; }
    public EvaluationRunStatus Status { get; private set; }
    public EvaluationValidity Validity { get; private set; }
    public string? InvalidReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<EvaluationCaseResult> CaseResults => _caseResults.AsReadOnly();
    public IReadOnlyCollection<MetricResult> Metrics =>
        JsonSerializer.Deserialize<List<MetricResult>>(_metricsJson) ?? new List<MetricResult>();

    public bool CanComplete => !string.IsNullOrWhiteSpace(ManifestHash);

    public static Result<EvaluationRun> Create(
        string manifestId,
        string manifestHash,
        string configurationHash,
        string protocolVersion,
        string datasetVersion,
        string groundTruthVersion,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(manifestId))
        {
            return Result.Failure<EvaluationRun>(new DomainError("EvaluationRun.ManifestIdRequired", "Manifest id is required."));
        }

        if (string.IsNullOrWhiteSpace(manifestHash))
        {
            return Result.Failure<EvaluationRun>(new DomainError("EvaluationRun.ManifestHashRequired", "Manifest hash is required."));
        }

        if (string.IsNullOrWhiteSpace(configurationHash))
        {
            return Result.Failure<EvaluationRun>(new DomainError("EvaluationRun.ConfigurationHashRequired", "Configuration hash is required."));
        }

        return new EvaluationRun
        {
            Id = Guid.NewGuid(),
            ManifestId = manifestId.Trim(),
            ManifestHash = manifestHash.Trim(),
            ConfigurationHash = configurationHash.Trim(),
            ProtocolVersion = protocolVersion.Trim(),
            DatasetVersion = datasetVersion.Trim(),
            GroundTruthVersion = groundTruthVersion.Trim(),
            Status = EvaluationRunStatus.Queued,
            Validity = EvaluationValidity.ValidForClaim,
            CreatedAt = createdAt
        };
    }

    public void Start()
    {
        Status = EvaluationRunStatus.Running;
    }

    public Result AddCaseResult(EvaluationCaseResult caseResult)
    {
        if (caseResult.EvaluationRunId != Id)
        {
            return Result.Failure(new DomainError("EvaluationRun.CaseRunMismatch", "Case result belongs to a different evaluation run."));
        }

        if (_caseResults.Any(existing => existing.CaseId.Equals(caseResult.CaseId, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(new DomainError("EvaluationRun.DuplicateCaseId", "Case id must be unique within an evaluation run."));
        }

        _caseResults.Add(caseResult);
        return Result.Success();
    }

    public void SetComponentVersions(
        string? schemaVersion,
        string? sanitizerVersion,
        string? parserVersion,
        string? routingPolicyVersion,
        string? embeddingVersion,
        string? rankerVersion,
        string? trustPolicyVersion,
        string? sourceCommit,
        string? buildVersion)
    {
        SchemaVersion = Normalize(schemaVersion);
        SanitizerVersion = Normalize(sanitizerVersion);
        ParserVersion = Normalize(parserVersion);
        RoutingPolicyVersion = Normalize(routingPolicyVersion);
        EmbeddingVersion = Normalize(embeddingVersion);
        RankerVersion = Normalize(rankerVersion);
        TrustPolicyVersion = Normalize(trustPolicyVersion);
        SourceCommit = Normalize(sourceCommit);
        BuildVersion = Normalize(buildVersion);

        if (new[] { SchemaVersion, SanitizerVersion, ParserVersion, RoutingPolicyVersion, EmbeddingVersion, RankerVersion, TrustPolicyVersion }
            .Any(string.IsNullOrWhiteSpace))
        {
            MarkInvalid(InvalidReasonCode.MissingComponentVersion);
        }
    }

    public void MarkInvalid(InvalidReasonCode reason)
    {
        Validity = EvaluationValidity.InvalidForClaim;
        InvalidReason = reason.ToString();
    }

    public Result Complete(IReadOnlyCollection<MetricResult> metrics, DateTimeOffset completedAt)
    {
        if (!CanComplete)
        {
            Status = EvaluationRunStatus.CompletedWithErrors;
            MarkInvalid(InvalidReasonCode.MissingManifestHash);
            CompletedAt = completedAt;
            return Result.Failure(new DomainError("EvaluationRun.ManifestHashRequired", "Manifest hash is required to complete an evaluation run."));
        }

        _metricsJson = JsonSerializer.Serialize(metrics);
        Status = _caseResults.Any(result => result.Outcome == EvaluationCaseOutcome.Failed)
            ? EvaluationRunStatus.CompletedWithErrors
            : EvaluationRunStatus.Completed;
        CompletedAt = completedAt;
        return Result.Success();
    }

    public void Fail(string errorCode, DateTimeOffset completedAt)
    {
        Status = EvaluationRunStatus.Failed;
        InvalidReason = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        CompletedAt = completedAt;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
