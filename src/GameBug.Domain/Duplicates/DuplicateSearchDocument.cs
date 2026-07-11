using GameBug.Domain.Analysis;

namespace GameBug.Domain.Duplicates;

public sealed record DuplicateSearchDocument(
    AnalysisRunId AnalysisRunId,
    Guid ProjectId,
    string Title,
    string ActualResult,
    string? TriggerAction,
    string? SceneOrFeature,
    string? StackSignature,
    string BuildVersion,
    string Platform,
    IReadOnlyList<string> GameEntities,
    string SearchText,
    string InputHash);
