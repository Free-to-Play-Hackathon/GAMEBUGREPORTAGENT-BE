namespace GameBug.Contracts.Evaluations;

public sealed record EvaluationCaseResponse(
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
