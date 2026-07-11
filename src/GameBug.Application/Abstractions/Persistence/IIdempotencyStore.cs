namespace GameBug.Application.Abstractions.Persistence;

public enum IdempotencyStatus
{
    Processing,
    Completed,
    Failed
}

public record IdempotencyRecord(
    string Key,
    string RequestHash,
    IdempotencyStatus Status,
    Guid? ReportId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiryTime);

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(IdempotencyRecord record, CancellationToken cancellationToken);
    Task UpdateAsync(IdempotencyRecord record, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
