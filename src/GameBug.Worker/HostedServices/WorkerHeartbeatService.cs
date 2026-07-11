using GameBug.Application.Abstractions.Persistence;
using GameBug.Infrastructure.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameBug.Worker.HostedServices;

public sealed class WorkerHeartbeatService : BackgroundService
{
    private const string WorkerName = "analysis-worker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EvaluationOptions _options;
    private readonly ILogger<WorkerHeartbeatService> _logger;

    public WorkerHeartbeatService(
        IServiceScopeFactory scopeFactory,
        IOptions<EvaluationOptions> options,
        ILogger<WorkerHeartbeatService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatStore>();
                await store.UpsertAsync(WorkerName, DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker heartbeat update failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.WorkerHeartbeatIntervalSeconds), stoppingToken);
        }
    }
}
