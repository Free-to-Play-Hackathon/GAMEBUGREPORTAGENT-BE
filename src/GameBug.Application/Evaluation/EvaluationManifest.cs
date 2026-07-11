namespace GameBug.Application.Evaluation;

public sealed record EvaluationManifest(
    string ManifestId,
    string ProtocolVersion,
    string DatasetVersion,
    string GroundTruthVersion,
    IReadOnlyList<EvaluationManifestCase> Cases);

public sealed record EvaluationManifestCase(
    string CaseId,
    string Split,
    string Type);

public sealed record EvaluationGroundTruth(
    string GroundTruthVersion,
    IReadOnlyList<EvaluationGroundTruthEntry> Entries);

public sealed record EvaluationGroundTruthEntry(
    string CaseId,
    string? ExpectedDuplicateKey,
    int? ExpectedRank);

public sealed record EvaluationCaseFixture(
    string CaseId,
    string Description,
    string? BuildVersion,
    string? Platform,
    string? Device,
    string? Locale,
    string? SessionReference,
    string? CrashLogText);
