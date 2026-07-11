using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Duplicates;
using GameBug.Application.HistoricalTickets.ImportHistoricalTickets;
using GameBug.Domain.Duplicates;
using GameBug.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Jobs;

public sealed class IndexHistoricalTicketJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IndexHistoricalTicketJob> _logger;

    public IndexHistoricalTicketJob(
        IServiceScopeFactory scopeFactory,
        ILogger<IndexHistoricalTicketJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IndexHistoricalTicketJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            HistoricalTicketIndexWorkItem? workItem = null;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IHistoricalTicketIndexQueue>();
                workItem = await queue.ClaimNextAsync(Environment.MachineName, stoppingToken);
                if (workItem is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                await ProcessTicketAsync(scope.ServiceProvider, workItem.TicketId, stoppingToken);
                await queue.CompleteAsync(workItem.JobId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ticket in IndexHistoricalTicketJob.");
                if (workItem is not null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var queue = scope.ServiceProvider.GetRequiredService<IHistoricalTicketIndexQueue>();
                    await queue.RetryAsync(workItem.JobId, "HISTORICAL_TICKET_INDEX_FAILED", CancellationToken.None);
                }
            }
        }

        _logger.LogInformation("IndexHistoricalTicketJob stopped.");
    }

    private async Task ProcessTicketAsync(IServiceProvider services, Guid ticketId, CancellationToken cancellationToken)
    {
        var tickets = services.GetRequiredService<IHistoricalTicketRepository>();
        var embeddingProvider = services.GetRequiredService<IEmbeddingProvider>();
        var dbContext = services.GetRequiredService<GameBugDbContext>();
        var options = services.GetRequiredService<IOptions<EmbeddingOptions>>().Value;

        var ticket = await tickets.GetByIdAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            _logger.LogWarning("Ticket {TicketId} not found during indexing.", ticketId);
            return;
        }

        var cache = await dbContext.EmbeddingCacheEntries.FirstOrDefaultAsync(
            entry =>
                entry.ContentHash == ticket.SearchTextHash &&
                entry.Provider == options.Provider &&
                entry.Model == options.Model &&
                entry.EmbeddingVersion == options.Version,
            cancellationToken);

        if (cache is not null && cache.Dimension == options.Dimension)
        {
            cache.MarkUsed(DateTimeOffset.UtcNow);
            ticket.SetEmbedding(cache.Vector, cache.Provider, cache.Model, cache.EmbeddingVersion, cache.Dimension, DateTimeOffset.UtcNow);
        }
        else
        {
            var embeddingResult = await embeddingProvider.EmbedAsync(ticket.SearchText, cancellationToken);
            ticket.SetEmbedding(
                embeddingResult.Vector,
                embeddingResult.Provider,
                embeddingResult.Model,
                embeddingResult.Version,
                embeddingResult.Dimension,
                DateTimeOffset.UtcNow);

            await dbContext.EmbeddingCacheEntries.AddAsync(
                new EmbeddingCacheEntry(
                    Guid.NewGuid(),
                    ticket.SearchTextHash,
                    embeddingResult.Provider,
                    embeddingResult.Model,
                    embeddingResult.Version,
                    embeddingResult.Vector,
                    embeddingResult.Dimension,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully indexed ticket {TicketId}.", ticketId);
    }
}
