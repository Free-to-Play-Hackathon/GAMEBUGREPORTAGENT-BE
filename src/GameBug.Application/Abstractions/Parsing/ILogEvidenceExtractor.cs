using GameBug.Domain.Evidence;

namespace GameBug.Application.Abstractions.Parsing;

public record ParsedTimelineEvent(
    DateTimeOffset? Timestamp,
    string EventName,
    string Excerpt,
    int LineNumber);

public record ParsedLogResult(
    string? ExceptionType,
    string? ExceptionMessage,
    List<string> StackFrames,
    string? BuildVersion,
    string? Platform,
    List<ParsedTimelineEvent> TimelineEvents,
    StackSignature? StackSignature);

public interface ILogEvidenceExtractor
{
    Task<ParsedLogResult> ExtractAsync(Stream logStream, CancellationToken cancellationToken);
}
