namespace GameBug.Domain.GameContext;

public class ExpectedBehavior
{
    // For EF Core
    private ExpectedBehavior() { }

    public ExpectedBehavior(
        Guid id,
        string trigger,
        string expectedOutcome,
        string source,
        string? buildRangeStart,
        string? buildRangeEnd)
    {
        Id = id;
        Trigger = trigger;
        ExpectedOutcome = expectedOutcome;
        Source = source;
        BuildRangeStart = buildRangeStart;
        BuildRangeEnd = buildRangeEnd;
    }

    public Guid Id { get; private set; }
    public string Trigger { get; private set; } = null!;
    public string ExpectedOutcome { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public string? BuildRangeStart { get; private set; }
    public string? BuildRangeEnd { get; private set; }
}
