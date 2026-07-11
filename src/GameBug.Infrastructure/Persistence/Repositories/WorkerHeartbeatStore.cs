using GameBug.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public sealed class WorkerHeartbeatStore : IWorkerHeartbeatStore
{
    private readonly GameBugDbContext _dbContext;

    public WorkerHeartbeatStore(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(string workerName, DateTimeOffset heartbeatAt, CancellationToken cancellationToken)
    {
        var heartbeat = await _dbContext.WorkerHeartbeats.FindAsync([workerName], cancellationToken);
        if (heartbeat is null)
        {
            await _dbContext.WorkerHeartbeats.AddAsync(new WorkerHeartbeat(workerName, heartbeatAt), cancellationToken);
        }
        else
        {
            heartbeat.Refresh(heartbeatAt);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastHeartbeatAsync(string workerName, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkerHeartbeats
            .Where(h => h.WorkerName == workerName)
            .Select(h => (DateTimeOffset?)h.LastHeartbeatAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
