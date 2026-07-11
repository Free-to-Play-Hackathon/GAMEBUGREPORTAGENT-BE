using System.Data;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.BugReports;
using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.GameContext;
using GameBug.Domain.QaWorkflow;
using GameBug.Infrastructure.HistoricalTickets;
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
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<AnalysisAiExecution> AnalysisAiExecutions => Set<AnalysisAiExecution>();
    public DbSet<AnalysisOutboxMessage> AnalysisOutboxMessages => Set<AnalysisOutboxMessage>();
    public DbSet<AnalysisJob> AnalysisJobs => Set<AnalysisJob>();
    public DbSet<AnalysisCheckpoint> AnalysisCheckpoints => Set<AnalysisCheckpoint>();
    public DbSet<AnalysisAttempt> AnalysisAttempts => Set<AnalysisAttempt>();
    public DbSet<AnalysisExecutionLease> AnalysisExecutionLeases => Set<AnalysisExecutionLease>();
    public DbSet<EvidenceFact> EvidenceFacts => Set<EvidenceFact>();
    public DbSet<EvidenceSource> EvidenceSources => Set<EvidenceSource>();
    public DbSet<EventTimelineEntry> EventTimelineEntries => Set<EventTimelineEntry>();
    public DbSet<ReproCase> ReproCases => Set<ReproCase>();
    public DbSet<ReproStep> ReproSteps => Set<ReproStep>();
    public DbSet<GameEntity> GameEntities => Set<GameEntity>();
    public DbSet<ExpectedBehavior> ExpectedBehaviors => Set<ExpectedBehavior>();
    public DbSet<HistoricalTicket> HistoricalTickets => Set<HistoricalTicket>();
    public DbSet<TicketImportBatch> TicketImportBatches => Set<TicketImportBatch>();
    public DbSet<EmbeddingCacheEntry> EmbeddingCacheEntries => Set<EmbeddingCacheEntry>();
    public DbSet<DuplicateMatch> DuplicateMatches => Set<DuplicateMatch>();
    public DbSet<HistoricalTicketIndexJobEntity> HistoricalTicketIndexJobs => Set<HistoricalTicketIndexJobEntity>();
    public DbSet<QaReview> QaReviews => Set<QaReview>();
    public DbSet<ReproRevision> ReproRevisions => Set<ReproRevision>();
    public DbSet<QaDecision> QaDecisions => Set<QaDecision>();
    public DbSet<ClarificationRequest> ClarificationRequests => Set<ClarificationRequest>();
    public DbSet<ClarificationQuestion> ClarificationQuestions => Set<ClarificationQuestion>();
    public DbSet<ClarificationAnswer> ClarificationAnswers => Set<ClarificationAnswer>();
    public DbSet<InternalTicket> InternalTickets => Set<InternalTicket>();
    public DbSet<TicketFilingRequest> TicketFilingRequests => Set<TicketFilingRequest>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
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

    public void ClearChanges() => ChangeTracker.Clear();
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
