namespace GameBug.Domain.GameContext;

public class GameEntity
{
    // For EF Core
    private GameEntity() { }

    public GameEntity(
        Guid id,
        string canonicalName,
        string[] aliases,
        string type,
        string? buildRangeStart,
        string? buildRangeEnd)
    {
        Id = id;
        CanonicalName = canonicalName;
        Aliases = aliases;
        Type = type;
        BuildRangeStart = buildRangeStart;
        BuildRangeEnd = buildRangeEnd;
    }

    public Guid Id { get; private set; }
    public string CanonicalName { get; private set; } = null!;
    public string[] Aliases { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string? BuildRangeStart { get; private set; }
    public string? BuildRangeEnd { get; private set; }
}
