using GameBug.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public class IdempotencyStore : IIdempotencyStore
{
    private readonly GameBugDbContext _dbContext;

    public IdempotencyStore(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.IdempotencyRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return new IdempotencyRecord(
            entity.Key,
            entity.RequestHash,
            Enum.Parse<IdempotencyStatus>(entity.Status),
            entity.ReportId,
            entity.CreatedAt,
            entity.ExpiryTime);
    }

    public async Task<bool> TryAddAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        int inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO idempotency_requests
                (key, request_hash, status, report_id, created_at, expiry_time)
            VALUES
                ({record.Key}, {record.RequestHash}, {record.Status.ToString()}, {record.ReportId}, {record.CreatedAt}, {record.ExpiryTime})
            ON CONFLICT (key) DO NOTHING
            """, cancellationToken);

        return inserted == 1;
    }

    public async Task UpdateAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.IdempotencyRequests
            .FirstOrDefaultAsync(x => x.Key == record.Key, cancellationToken);

        if (entity != null)
        {
            entity.RequestHash = record.RequestHash;
            entity.Status = record.Status.ToString();
            entity.ReportId = record.ReportId;
            entity.ExpiryTime = record.ExpiryTime;
            _dbContext.IdempotencyRequests.Update(entity);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.IdempotencyRequests
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (entity != null)
        {
            _dbContext.IdempotencyRequests.Remove(entity);
        }
    }
}
