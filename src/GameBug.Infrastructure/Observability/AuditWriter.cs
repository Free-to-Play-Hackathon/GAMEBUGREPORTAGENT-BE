using System.Text.Json;
using GameBug.Application.Abstractions.Observability;
using GameBug.Infrastructure.Persistence;

namespace GameBug.Infrastructure.Observability;

public class AuditWriter : IAuditWriter
{
    private readonly GameBugDbContext _dbContext;

    public AuditWriter(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string entityType,
        Guid entityId,
        string action,
        string actor,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        string? metadataJson = metadata != null
            ? JsonSerializer.Serialize(metadata)
            : null;

        var auditEvent = new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            MetadataJson = metadataJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
    }
}
