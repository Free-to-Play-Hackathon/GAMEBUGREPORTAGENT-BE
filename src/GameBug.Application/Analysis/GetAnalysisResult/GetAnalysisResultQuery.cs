using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.GetAnalysisResult;

public record GetAnalysisResultQuery(Guid AnalysisId) : IRequest<Result<GetAnalysisResultDetails>>;

public record GetAnalysisResultDetails(
    Guid AnalysisId,
    List<EvidenceFactDto> Facts,
    List<EventTimelineEntryDto> Timeline,
    ReproCaseDto ReproCase,
    IReadOnlyList<DuplicateCandidateDto> DuplicateCandidates,
    string? CandidateSnapshotHash,
    IReadOnlyList<string> Warnings,
    AnalysisMetadataDto AnalysisMetadata,
    TrustSummaryDto? Trust);

public record AnalysisMetadataDto(
    int Version,
    string SchemaVersion,
    string? SanitizerVersion,
    string? ParserVersion,
    string? PromptVersion,
    string? ModelProvider,
    string? ModelName,
    string? EmbeddingModel,
    string? EmbeddingVersion,
    string? RankerVersion,
    string? RerankerModel);

public record DuplicateCandidateDto(
    string TicketId,
    int Rank,
    double Score,
    string Classification,
    string Reason,
    IReadOnlyList<string> MatchingSignals,
    IReadOnlyList<string> ConflictingSignals,
    DuplicateScoreBreakdownDto ScoreBreakdown);

public record DuplicateScoreBreakdownDto(
    double? StackSignature,
    double? SemanticText,
    double? TriggerAction,
    double? SceneOrFeature,
    double? ActualResult,
    double? BuildPlatform,
    double? ScreenshotContext);

public record EvidenceFactDto(Guid Id, string FactType, string? NormalizedValue, string Status, double Confidence, List<EvidenceSourceDto> Sources);
public record EvidenceSourceDto(Guid Id, string SourceType, string SourceRef, int? LineStart, int? LineEnd, string SanitizedExcerpt, string ExcerptHash, string TrustLevel);
public record EventTimelineEntryDto(Guid Id, DateTimeOffset? Timestamp, int RelativeSequence, string EventName, string Excerpt, string ExcerptHash, string SourceRef, int? SourceLine);
public record ReproCaseDto(Guid Id, string Title, string BuildVersion, string Platform, string Preconditions, List<ReproStepDto> Steps, string ExpectedResult, string ActualResult, string SeverityEstimate, string SeverityReason, string? MissingInformation, double Confidence);
public record ReproStepDto(Guid Id, int Order, string Description, string StepType, Guid? SourceId, string? InferenceReason);
public record TrustSummaryDto(
    string QualityOutcome,
    string PolicyVersion,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<TrustViolationDto> Violations,
    DateTimeOffset EvaluatedAt);
public record TrustViolationDto(string Code, string OutputPath, Guid? SourceId, bool IsBlocking, string Message);
