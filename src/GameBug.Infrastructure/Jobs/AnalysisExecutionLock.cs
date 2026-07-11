using GameBug.Application.Abstractions.Jobs;
using GameBug.Domain.Analysis;
using GameBug.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Jobs;

public sealed class AnalysisExecutionLock : IAnalysisExecutionLock
{
    private readonly GameBugDbContext _dbContext;

    public AnalysisExecutionLock(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryAcquireAsync(
        AnalysisRunId analysisRunId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lockedUntil = now.Add(leaseDuration);
        var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO analysis_execution_locks
                (analysis_run_id, locked_by, locked_until, updated_at)
            VALUES
                ({analysisRunId.Value}, {workerId}, {lockedUntil}, {now})
            ON CONFLICT (analysis_run_id) DO UPDATE
            SET locked_by = EXCLUDED.locked_by,
                locked_until = EXCLUDED.locked_until,
                updated_at = EXCLUDED.updated_at
            WHERE analysis_execution_locks.locked_until < {now}
                OR analysis_execution_locks.locked_by = {workerId}
            """, cancellationToken);

        return rows == 1;
    }

    public async Task ReleaseAsync(
        AnalysisRunId analysisRunId,
        string workerId,
        CancellationToken cancellationToken)
    {
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM analysis_execution_locks
            WHERE analysis_run_id = {analysisRunId.Value}
                AND locked_by = {workerId}
            """, cancellationToken);
    }

    public async Task<bool> RenewAsync(
        AnalysisRunId analysisRunId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE analysis_execution_locks
            SET locked_until = {now.Add(leaseDuration)},
                updated_at = {now}
            WHERE analysis_run_id = {analysisRunId.Value}
                AND locked_by = {workerId}
                AND locked_until >= {now}
            """, cancellationToken);

        return rows == 1;
    }
}
