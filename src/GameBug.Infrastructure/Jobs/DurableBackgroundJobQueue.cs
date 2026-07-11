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
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO analysis_jobs
                (id, queue_name, analysis_run_id, expected_version, status, attempt_count,
                 available_at, locked_by, locked_until, created_at, completed_at, last_error_code)
            VALUES
                ({Guid.NewGuid()}, {_options.QueueName}, {analysisRunId.Value}, {expectedVersion}, 'Queued', 0,
                 {now}, NULL, NULL, {now}, NULL, NULL)
            ON CONFLICT (queue_name, analysis_run_id, expected_version) DO NOTHING
            """, cancellationToken);
    }

    public async Task<ClaimedAnalysisJob?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var candidates = await _dbContext.AnalysisJobs
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
            .ToListAsync(cancellationToken);
        var candidate = candidates.SingleOrDefault();

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

    public async Task<bool> RetryAsync(Guid jobId, string errorCode, CancellationToken cancellationToken)
    {
        var job = await _dbContext.AnalysisJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return false;
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
        return job.Status == "Queued";
    }

    public async Task<bool> RenewLeaseAsync(
        Guid jobId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE analysis_jobs
            SET locked_until = {now.Add(leaseDuration)}
            WHERE id = {jobId}
                AND status = 'Processing'
                AND locked_by = {workerId}
                AND locked_until >= {now}
            """, cancellationToken);

        return rows == 1;
    }
}
