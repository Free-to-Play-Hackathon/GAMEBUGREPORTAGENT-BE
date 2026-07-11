using System.Data;
using GameBug.Application.HistoricalTickets.ImportHistoricalTickets;
using GameBug.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.HistoricalTickets;

public sealed class HistoricalTicketIndexQueue : IHistoricalTicketIndexQueue
{
    private readonly GameBugDbContext _dbContext;

    public HistoricalTicketIndexQueue(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        bool alreadyQueued = await _dbContext.HistoricalTicketIndexJobs.AnyAsync(
            job => job.TicketId == ticketId && (job.Status == "Queued" || job.Status == "Processing"),
            cancellationToken);
        if (alreadyQueued)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await _dbContext.HistoricalTicketIndexJobs.AddAsync(
            new HistoricalTicketIndexJobEntity(Guid.NewGuid(), ticketId, now, now),
            cancellationToken);
    }

    public async Task<HistoricalTicketIndexWorkItem?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var candidate = await _dbContext.HistoricalTicketIndexJobs
            .FromSqlInterpolated($"""
                SELECT *
                FROM historical_ticket_index_jobs
                WHERE available_at <= {now}
                    AND (status = 'Queued' OR (status = 'Processing' AND locked_until < {now}))
                ORDER BY available_at, created_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        candidate.Claim(workerId, now.AddMinutes(5));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new HistoricalTicketIndexWorkItem(candidate.Id, candidate.TicketId, candidate.AttemptCount);
    }

    public async Task CompleteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.HistoricalTicketIndexJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Complete(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid jobId, string errorCode, CancellationToken cancellationToken)
    {
        var job = await _dbContext.HistoricalTicketIndexJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        if (job.AttemptCount >= 5)
        {
            job.DeadLetter(errorCode, DateTimeOffset.UtcNow);
        }
        else
        {
            int delaySeconds = job.AttemptCount switch
            {
                <= 1 => 2,
                2 => 10,
                _ => 30
            };
            job.Retry(errorCode, DateTimeOffset.UtcNow.AddSeconds(delaySeconds));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
