using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.GetAnalysisResult;

public record GetAnalysisResultQuery(Guid AnalysisId) : IRequest<Result<GetAnalysisResultDetails>>;

public record GetAnalysisResultDetails(
    Guid AnalysisId,
    List<EvidenceFactDto> Facts,
    List<EventTimelineEntryDto> Timeline,
    ReproCaseDto ReproCase,
    IReadOnlyList<object> DuplicateCandidates,
    IReadOnlyList<string> Warnings,
    AnalysisMetadataDto AnalysisMetadata);

public record AnalysisMetadataDto(
    int Version,
    string SchemaVersion,
    string? SanitizerVersion,
    string? ParserVersion,
    string? PromptVersion,
    string? ModelProvider,
    string? ModelName);

public record EvidenceFactDto(Guid Id, string FactType, string? NormalizedValue, string Status, double Confidence, List<EvidenceSourceDto> Sources);
public record EvidenceSourceDto(Guid Id, string SourceType, string SourceRef, int? LineStart, int? LineEnd, string SanitizedExcerpt, string ExcerptHash, string TrustLevel);
public record EventTimelineEntryDto(Guid Id, DateTimeOffset? Timestamp, int RelativeSequence, string EventName, string Excerpt, string ExcerptHash, string SourceRef, int? SourceLine);
public record ReproCaseDto(Guid Id, string Title, string BuildVersion, string Platform, string Preconditions, List<ReproStepDto> Steps, string ExpectedResult, string ActualResult, string SeverityEstimate, string SeverityReason, string? MissingInformation, double Confidence);
public record ReproStepDto(Guid Id, int Order, string Description, string StepType, Guid? SourceId, string? InferenceReason);
