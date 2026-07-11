using GameBug.Application.Abstractions.Jobs;
using GameBug.Domain.Analysis;
using GameBug.Infrastructure.Persistence;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Jobs;

public sealed record ClaimedAnalysisJob(Guid JobId, AnalysisRunId AnalysisRunId, int ExpectedVersion, int AttemptCount);

public sealed class DurableBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly GameBugDbContext _dbContext;
    private readonly JobOptions _options;

    public DurableBackgroundJobQueue(GameBugDbContext dbContext, IOptions<JobOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task EnqueueProcessAnalysisAsync(
        AnalysisRunId analysisRunId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await _dbContext.AnalysisJobs.AddAsync(
            new AnalysisJob(Guid.NewGuid(), _options.QueueName, analysisRunId, expectedVersion, now, now),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClaimedAnalysisJob?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var candidate = await _dbContext.AnalysisJobs
            .FromSqlInterpolated($"""
                SELECT *
                FROM analysis_jobs
                WHERE queue_name = {_options.QueueName}
                    AND available_at <= {now}
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

        candidate.Claim(workerId, now.AddSeconds(_options.LeaseDurationSeconds));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ClaimedAnalysisJob(candidate.Id, candidate.AnalysisRunId, candidate.ExpectedVersion, candidate.AttemptCount);
    }

    public async Task CompleteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.AnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Complete(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid jobId, string errorCode, CancellationToken cancellationToken)
    {
        var job = await _dbContext.AnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        if (job.AttemptCount >= _options.MaxAttempts)
        {
            job.DeadLetter(errorCode, DateTimeOffset.UtcNow);
        }
        else
        {
            var delaySeconds = job.AttemptCount switch
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
