namespace GameBug.Contracts.Evaluations;

public sealed record StartEvaluationRequest(
    string ManifestId,
    string Profile);
