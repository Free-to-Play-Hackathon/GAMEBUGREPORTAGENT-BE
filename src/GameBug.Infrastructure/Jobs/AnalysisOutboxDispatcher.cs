using System.Text.Json;
using GameBug.Application.Abstractions.Jobs;
using GameBug.Domain.Analysis;
using GameBug.Infrastructure.Persistence;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Jobs;

public sealed class AnalysisOutboxDispatcher : IOutboxDispatcher
{
    private readonly GameBugDbContext _dbContext;
    private readonly IBackgroundJobQueue _queue;
    private readonly JobOptions _options;
    private readonly ILogger<AnalysisOutboxDispatcher> _logger;

    public AnalysisOutboxDispatcher(
        GameBugDbContext dbContext,
        IBackgroundJobQueue queue,
        IOptions<JobOptions> options,
        ILogger<AnalysisOutboxDispatcher> logger)
    {
        _dbContext = dbContext;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var workerId = Environment.MachineName;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var messages = await _dbContext.AnalysisOutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM analysis_outbox
                WHERE (dispatch_status = 'Pending' OR (dispatch_status = 'Dispatching' AND locked_until < {now}))
                    AND next_attempt_at <= {now}
                ORDER BY occurred_at
                FOR UPDATE SKIP LOCKED
                LIMIT {_options.DispatcherBatchSize}
                """)
            .ToListAsync(cancellationToken);

        var dispatched = 0;
        foreach (var message in messages)
        {
            message.Claim(workerId, now.AddSeconds(_options.LeaseDurationSeconds));
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                if (!string.Equals(message.MessageType, "ProcessAnalysis", StringComparison.Ordinal))
                {
                    message.MarkFailed("OUTBOX_MESSAGE_TYPE_UNSUPPORTED", DateTimeOffset.UtcNow.AddMinutes(5));
                    continue;
                }

                var payload = JsonSerializer.Deserialize<ProcessAnalysisOutboxPayload>(message.PayloadJson);
                if (payload is null || payload.AnalysisRunId == Guid.Empty || payload.ExpectedVersion <= 0)
                {
                    message.MarkFailed("OUTBOX_PAYLOAD_INVALID", DateTimeOffset.UtcNow.AddMinutes(5));
                    continue;
                }

                await _queue.EnqueueProcessAnalysisAsync(
                    new AnalysisRunId(payload.AnalysisRunId),
                    payload.ExpectedVersion,
                    cancellationToken);

                message.MarkDispatched(DateTimeOffset.UtcNow);
                dispatched++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch outbox message {OutboxId}", message.Id);
                message.MarkFailed("OUTBOX_DISPATCH_FAILED", DateTimeOffset.UtcNow.AddSeconds(10));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return dispatched;
    }
}
