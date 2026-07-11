using GameBug.Application.Abstractions.Jobs;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;
using GameBug.Infrastructure.Jobs;
using GameBug.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Worker.Consumers;

public sealed class ProcessAnalysisJobService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobOptions _options;
    private readonly ILogger<ProcessAnalysisJobService> _logger;
    private readonly string _workerInstanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public ProcessAnalysisJobService(
        IServiceScopeFactory scopeFactory,
        IOptions<JobOptions> options,
        ILogger<ProcessAnalysisJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, _options.WorkerConcurrency)
            .Select(slot => RunWorkerAsync($"{_workerInstanceId}-{slot}", stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(string workerId, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<DurableBackgroundJobQueue>();
                var job = await queue.ClaimNextAsync(workerId, stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.DispatcherPollingIntervalSeconds), stoppingToken);
                    continue;
                }

                await ProcessJobAsync(scope.ServiceProvider, queue, job, workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analysis worker loop failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(
        IServiceProvider services,
        DurableBackgroundJobQueue queue,
        ClaimedAnalysisJob job,
        string workerId,
        CancellationToken stoppingToken)
    {
        var executionLock = services.GetRequiredService<IAnalysisExecutionLock>();
        var sender = services.GetRequiredService<ISender>();
        var dbContext = services.GetRequiredService<GameBugDbContext>();
        var lease = TimeSpan.FromSeconds(_options.LeaseDurationSeconds);

        var acquired = await executionLock.TryAcquireAsync(job.AnalysisRunId, workerId, lease, stoppingToken);
        if (!acquired)
        {
            await RetryOrFailRunAsync(queue, dbContext, job, "ANALYSIS_LOCK_BUSY", stoppingToken);
            return;
        }

        AnalysisAttempt? attempt = null;
        try
        {
            var runSnapshot = await dbContext.AnalysisRuns
                .AsNoTracking()
                .Where(run => run.Id == job.AnalysisRunId)
                .Select(run => new { run.Version })
                .SingleOrDefaultAsync(stoppingToken);
            if (runSnapshot is null || runSnapshot.Version != job.ExpectedVersion)
            {
                _logger.LogWarning(
                    "Discarding stale analysis job {JobId}; expected version {ExpectedVersion}",
                    job.JobId,
                    job.ExpectedVersion);
                await queue.CompleteAsync(job.JobId, stoppingToken);
                return;
            }

            attempt = new AnalysisAttempt(
                Guid.NewGuid(),
                job.AnalysisRunId,
                job.JobId,
                workerId,
                job.AttemptCount,
                DateTimeOffset.UtcNow);
            dbContext.AnalysisAttempts.Add(attempt);
            await dbContext.SaveChangesAsync(stoppingToken);

            using var processingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var heartbeatTask = RunLeaseHeartbeatAsync(
                job,
                workerId,
                lease,
                processingCts,
                processingCts.Token);

            _logger.LogInformation("Processing analysis job {JobId} for run {RunId}", job.JobId, job.AnalysisRunId.Value);
            Result result;
            try
            {
                result = await sender.Send(new ProcessAnalysisCommand(job.AnalysisRunId.Value), processingCts.Token);
            }
            finally
            {
                processingCts.Cancel();
                await IgnoreHeartbeatCancellationAsync(heartbeatTask);
            }

            if (result.IsSuccess)
            {
                attempt.Finish("Completed", null, DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(stoppingToken);
                await queue.CompleteAsync(job.JobId, stoppingToken);
                return;
            }

            attempt.Finish("Failed", result.Error.Code, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(stoppingToken);

            var run = await dbContext.AnalysisRuns.FirstOrDefaultAsync(
                item => item.Id == job.AnalysisRunId,
                stoppingToken);
            bool retryable = result.Error.Code == "INTERRUPTED" ||
                             run?.FailureCategory?.StartsWith("Transient", StringComparison.Ordinal) == true;
            if (retryable)
            {
                await RetryOrFailRunAsync(queue, dbContext, job, result.Error.Code, stoppingToken);
            }
            else
            {
                await queue.CompleteAsync(job.JobId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (attempt is not null)
            {
                attempt.Finish("Interrupted", "INTERRUPTED", DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            await RetryOrFailRunAsync(queue, dbContext, job, "INTERRUPTED", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure processing analysis job {JobId}", job.JobId);
            if (attempt is not null)
            {
                attempt.Finish("Failed", "WORKER_UNHANDLED_FAILURE", DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            await RetryOrFailRunAsync(queue, dbContext, job, "WORKER_UNHANDLED_FAILURE", stoppingToken);
        }
        finally
        {
            await executionLock.ReleaseAsync(job.AnalysisRunId, workerId, CancellationToken.None);
        }
    }

    private async Task RunLeaseHeartbeatAsync(
        ClaimedAnalysisJob job,
        string workerId,
        TimeSpan lease,
        CancellationTokenSource processingCts,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds), cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var executionLock = scope.ServiceProvider.GetRequiredService<IAnalysisExecutionLock>();
            var queue = scope.ServiceProvider.GetRequiredService<DurableBackgroundJobQueue>();
            var dbContext = scope.ServiceProvider.GetRequiredService<GameBugDbContext>();

            bool executionRenewed = await executionLock.RenewAsync(
                job.AnalysisRunId,
                workerId,
                lease,
                cancellationToken);
            bool jobRenewed = await queue.RenewLeaseAsync(
                job.JobId,
                workerId,
                lease,
                cancellationToken);

            if (!executionRenewed || !jobRenewed)
            {
                _logger.LogError("Lease lost for analysis job {JobId}; cancelling local processing", job.JobId);
                processingCts.Cancel();
                return;
            }

            var heartbeatAt = DateTimeOffset.UtcNow;
            await dbContext.AnalysisRuns
                .Where(run => run.Id == job.AnalysisRunId)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(run => run.LastHeartbeatAt, heartbeatAt),
                    cancellationToken);
        }
    }

    private static async Task IgnoreHeartbeatCancellationAsync(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task RetryOrFailRunAsync(
        DurableBackgroundJobQueue queue,
        GameBugDbContext dbContext,
        ClaimedAnalysisJob job,
        string errorCode,
        CancellationToken cancellationToken)
    {
        bool scheduled = await queue.RetryAsync(job.JobId, errorCode, cancellationToken);
        if (scheduled)
        {
            return;
        }

        var run = await dbContext.AnalysisRuns.FirstOrDefaultAsync(
            item => item.Id == job.AnalysisRunId,
            cancellationToken);
        if (run is null || run.IsTerminal)
        {
            return;
        }

        run.Fail(
            "MAX_ATTEMPTS_EXCEEDED",
            new[] { new AnalysisWarning(errorCode, "The analysis exhausted its bounded retry attempts.") },
            DateTimeOffset.UtcNow,
            "PermanentRetryExhausted");
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
