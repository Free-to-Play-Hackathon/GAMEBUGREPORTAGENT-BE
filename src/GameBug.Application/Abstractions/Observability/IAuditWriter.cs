namespace GameBug.Application.Abstractions.Observability;

public interface IAuditWriter
{
    Task WriteAsync(
        string entityType,
        Guid entityId,
        string action,
        string actor,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
