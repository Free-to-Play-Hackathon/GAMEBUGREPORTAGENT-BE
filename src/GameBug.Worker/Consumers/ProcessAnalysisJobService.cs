using GameBug.Application.Abstractions.Jobs;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Domain.Analysis;
using GameBug.Infrastructure.Jobs;
using GameBug.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameBug.Worker.Consumers;

public sealed class ProcessAnalysisJobService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobOptions _options;
    private readonly ILogger<ProcessAnalysisJobService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

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
            .Select(_ => RunWorkerAsync(stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<DurableBackgroundJobQueue>();
                var job = await queue.ClaimNextAsync(_workerId, stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.DispatcherPollingIntervalSeconds), stoppingToken);
                    continue;
                }

                await ProcessJobAsync(scope.ServiceProvider, queue, job, stoppingToken);
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
        CancellationToken stoppingToken)
    {
        var executionLock = services.GetRequiredService<IAnalysisExecutionLock>();
        var sender = services.GetRequiredService<ISender>();
        var dbContext = services.GetRequiredService<GameBugDbContext>();
        var lease = TimeSpan.FromSeconds(_options.LeaseDurationSeconds);

        var acquired = await executionLock.TryAcquireAsync(job.AnalysisRunId, _workerId, lease, stoppingToken);
        if (!acquired)
        {
            await queue.RetryAsync(job.JobId, "ANALYSIS_LOCK_BUSY", stoppingToken);
            return;
        }

        var attempt = new AnalysisAttempt(Guid.NewGuid(), job.AnalysisRunId, job.JobId, _workerId, job.AttemptCount, DateTimeOffset.UtcNow);
        dbContext.AnalysisAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(stoppingToken);

        try
        {
            _logger.LogInformation("Processing analysis job {JobId} for run {RunId}", job.JobId, job.AnalysisRunId.Value);
            var result = await sender.Send(new ProcessAnalysisCommand(job.AnalysisRunId.Value), stoppingToken);
            if (result.IsSuccess)
            {
                attempt.Finish("Completed", null, DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(stoppingToken);
                await queue.CompleteAsync(job.JobId, stoppingToken);
                return;
            }

            attempt.Finish("Failed", result.Error.Code, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(stoppingToken);

            if (result.Error.Code == "INTERRUPTED")
            {
                await queue.RetryAsync(job.JobId, result.Error.Code, stoppingToken);
            }
            else
            {
                await queue.CompleteAsync(job.JobId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            attempt.Finish("Interrupted", "INTERRUPTED", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await queue.RetryAsync(job.JobId, "INTERRUPTED", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure processing analysis job {JobId}", job.JobId);
            attempt.Finish("Failed", "WORKER_UNHANDLED_FAILURE", DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(stoppingToken);
            await queue.RetryAsync(job.JobId, "WORKER_UNHANDLED_FAILURE", stoppingToken);
        }
        finally
        {
            await executionLock.ReleaseAsync(job.AnalysisRunId, _workerId, CancellationToken.None);
        }
    }
}
