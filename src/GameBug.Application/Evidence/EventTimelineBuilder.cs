using GameBug.Application.Abstractions.Parsing;
using GameBug.Domain.Evidence;

namespace GameBug.Application.Evidence;

public class EventTimelineBuilder
{
    public List<EventTimelineEntry> BuildTimeline(
        List<ParsedTimelineEvent> parsedEvents,
        string logSourceRef)
    {
        // Sort by timestamp then line number
        var sortedParsedEvents = parsedEvents
            .OrderBy(e => e.Timestamp ?? DateTimeOffset.MinValue)
            .ThenBy(e => e.LineNumber)
            .ToList();

        var timeline = new List<EventTimelineEntry>();
        for (int i = 0; i < sortedParsedEvents.Count; i++)
        {
            var pe = sortedParsedEvents[i];
            var excerptHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pe.Excerpt))
            ).ToLowerInvariant();

            timeline.Add(new EventTimelineEntry(
                Guid.NewGuid(),
                pe.Timestamp,
                relativeSequence: i + 1,
                eventName: pe.EventName,
                excerpt: pe.Excerpt,
                excerptHash: excerptHash,
                sourceRef: pe.SourceRef ?? logSourceRef,
                sourceLine: pe.LineNumber
            ));
        }

        return timeline;
    }
}
