using GameBug.Domain.GameContext;

namespace GameBug.Application.Abstractions.Persistence;

public interface IGameContextRepository
{
    Task<GameEntity?> FindEntityByAliasAsync(string alias, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<GameEntity>> GetGameEntitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExpectedBehavior>> GetExpectedBehaviorsAsync(CancellationToken cancellationToken);
    Task SeedGameContextAsync(IEnumerable<GameEntity> entities, IEnumerable<ExpectedBehavior> behaviors, CancellationToken cancellationToken);
}
