namespace GameBug.Domain.Evidence;

public class EventTimelineEntry
{
    // For EF Core
    private EventTimelineEntry() { }

    public EventTimelineEntry(
        Guid id,
        DateTimeOffset? timestamp,
        int relativeSequence,
        string eventName,
        string excerpt,
        string excerptHash,
        string sourceRef,
        int? sourceLine)
    {
        Id = id;
        Timestamp = timestamp;
        RelativeSequence = relativeSequence;
        EventName = eventName;
        Excerpt = excerpt;
        ExcerptHash = excerptHash;
        SourceRef = sourceRef;
        SourceLine = sourceLine;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset? Timestamp { get; private set; }
    public int RelativeSequence { get; private set; }
    public string EventName { get; private set; } = null!;
    public string Excerpt { get; private set; } = null!;
    public string ExcerptHash { get; private set; } = null!;
    public string SourceRef { get; private set; } = null!;
    public int? SourceLine { get; private set; }
}
