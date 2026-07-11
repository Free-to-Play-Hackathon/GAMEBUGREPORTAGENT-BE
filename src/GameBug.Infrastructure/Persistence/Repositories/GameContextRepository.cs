using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.GameContext;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public class GameContextRepository : IGameContextRepository
{
    private readonly GameBugDbContext _dbContext;

    public GameContextRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GameEntity?> FindEntityByAliasAsync(string alias, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        string normalizedAlias = alias.Trim().ToLowerInvariant();

        return await _dbContext.GameEntities
            .FirstOrDefaultAsync(x =>
                x.CanonicalName.ToLower() == normalizedAlias ||
                x.Aliases.Any(a => a.ToLower() == normalizedAlias),
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExpectedBehavior>> GetExpectedBehaviorsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ExpectedBehaviors.ToListAsync(cancellationToken);
    }

    public async Task SeedGameContextAsync(IEnumerable<GameEntity> entities, IEnumerable<ExpectedBehavior> behaviors, CancellationToken cancellationToken)
    {
        // Simple seed logic
        if (!await _dbContext.GameEntities.AnyAsync(cancellationToken))
        {
            await _dbContext.GameEntities.AddRangeAsync(entities, cancellationToken);
        }

        if (!await _dbContext.ExpectedBehaviors.AnyAsync(cancellationToken))
        {
            await _dbContext.ExpectedBehaviors.AddRangeAsync(behaviors, cancellationToken);
        }
    }
}
