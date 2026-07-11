using System.Data;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.BugReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameBug.Infrastructure.Persistence;

public class GameBugDbContext : DbContext, IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;

    public GameBugDbContext(DbContextOptions<GameBugDbContext> options)
        : base(options)
    {
    }

    public DbSet<BugReport> BugReports => Set<BugReport>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<IdempotencyRequestEntity> IdempotencyRequests => Set<IdempotencyRequestEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameBugDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }
}

public class IdempotencyRequestEntity
{
    public string Key { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid? ReportId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiryTime { get; set; }
}

public class AuditEventEntity
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = null!;
    public string Actor { get; set; } = null!;
    public string? MetadataJson { get; set; } // string-serialized json or jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
