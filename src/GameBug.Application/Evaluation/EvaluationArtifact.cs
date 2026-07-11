using GameBug.Application.Evaluation.GetEvaluation;

namespace GameBug.Application.Evaluation;

public sealed record EvaluationArtifact(
    Guid RunId,
    string ManifestId,
    string ManifestHash,
    string ConfigurationHash,
    string Validity,
    EvaluationComponentVersionResult ComponentVersions,
    IReadOnlyCollection<EvaluationMetricResult> Metrics,
    IReadOnlyCollection<EvaluationCaseResultDto> Cases);
