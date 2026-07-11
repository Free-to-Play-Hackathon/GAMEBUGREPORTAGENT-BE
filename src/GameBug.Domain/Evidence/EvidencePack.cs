using GameBug.Domain.Analysis;

namespace GameBug.Domain.Evidence;

public class EvidencePack
{
    private readonly List<EvidenceFact> _facts = new();
    private readonly List<EventTimelineEntry> _timeline = new();

    // For EF Core
    private EvidencePack() { }

    public EvidencePack(
        Guid id,
        AnalysisRunId analysisRunId,
        IEnumerable<EvidenceFact> facts,
        IEnumerable<EventTimelineEntry> timeline)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        _facts.AddRange(facts);
        _timeline.AddRange(timeline);
    }

    public Guid Id { get; private set; }
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;

    public IReadOnlyCollection<EvidenceFact> Facts => _facts.AsReadOnly();
    public IReadOnlyCollection<EventTimelineEntry> Timeline => _timeline.AsReadOnly();
}
