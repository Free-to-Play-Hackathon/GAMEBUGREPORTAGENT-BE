using GameBug.Domain.Evidence;

namespace GameBug.Application.Abstractions.Parsing;

public record ParsedTimelineEvent(
    DateTimeOffset? Timestamp,
    string EventName,
    string Excerpt,
    int LineNumber,
    string? SourceRef = null);

public sealed record GameplayLogFacts(
    string? FormatVersion,
    string? Screen,
    string? Action,
    string? ResourceType,
    decimal? ResourceBefore,
    decimal? ResourceAfter,
    int? ExpectedRewardCount,
    int? ReceivedRewardCount,
    string? ServerResponse,
    string? ErrorCode);

public record ParsedLogResult(
    string? ExceptionType,
    string? ExceptionMessage,
    List<string> StackFrames,
    string? BuildVersion,
    string? Platform,
    List<ParsedTimelineEvent> TimelineEvents,
    StackSignature? StackSignature,
    GameplayLogFacts? GameplayFacts = null);

public interface ILogEvidenceExtractor
{
    Task<ParsedLogResult> ExtractAsync(Stream logStream, CancellationToken cancellationToken);
}
