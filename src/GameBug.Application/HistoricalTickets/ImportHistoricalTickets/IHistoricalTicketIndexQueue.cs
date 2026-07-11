namespace GameBug.Application.HistoricalTickets.ImportHistoricalTickets;

public interface IHistoricalTicketIndexQueue
{
    Task EnqueueAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<HistoricalTicketIndexWorkItem?> ClaimNextAsync(string workerId, CancellationToken cancellationToken);
    Task CompleteAsync(Guid jobId, CancellationToken cancellationToken);
    Task RetryAsync(Guid jobId, string errorCode, CancellationToken cancellationToken);
}

public sealed record HistoricalTicketIndexWorkItem(Guid JobId, Guid TicketId, int AttemptCount);
