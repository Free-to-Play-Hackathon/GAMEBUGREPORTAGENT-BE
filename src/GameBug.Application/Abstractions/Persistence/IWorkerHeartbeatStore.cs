namespace GameBug.Application.Abstractions.Persistence;

public interface IWorkerHeartbeatStore
{
    Task UpsertAsync(string workerName, DateTimeOffset heartbeatAt, CancellationToken cancellationToken);
    Task<DateTimeOffset?> GetLastHeartbeatAsync(string workerName, CancellationToken cancellationToken);
}
