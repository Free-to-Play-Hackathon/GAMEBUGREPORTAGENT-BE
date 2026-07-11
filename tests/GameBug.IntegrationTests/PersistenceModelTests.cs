using FluentAssertions;
using GameBug.Domain.BugReports;
using GameBug.Infrastructure.Persistence;
using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameBug.IntegrationTests;

public sealed class PersistenceModelTests
{
    private static GameBugDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GameBugDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test", x => x.UseVector())
            .Options;
        return new GameBugDbContext(options);
    }

    [Fact]
    public void PhaseTwoModel_ShouldContainHardeningConstraintsAndActiveRunIndex()
    {
        using var context = CreateContext();
        var run = context.Model.FindEntityType(typeof(AnalysisRun));

        run.Should().NotBeNull();
        run!.FindProperty(nameof(AnalysisRun.ResultReference)).Should().NotBeNull();
        run.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.GetFilter() != null &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(AnalysisRun.ReportId),
                    nameof(AnalysisRun.InputHash),
                    nameof(AnalysisRun.ConfigurationHash)
                }));
    }

    [Fact]
    public void Model_ShouldKeepStorageKeyUniqueAndVersionConcurrent()
    {
        using var context = CreateContext();
        var attachment = context.Model.FindEntityType(typeof(Attachment));
        var report = context.Model.FindEntityType(typeof(BugReport));

        attachment.Should().NotBeNull();
        attachment!.GetIndexes().Single(index =>
            index.Properties.Single().Name == nameof(Attachment.StorageKey)).IsUnique.Should().BeTrue();
        report!.FindProperty(nameof(BugReport.Version))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void Model_ShouldMapAllPhaseOneTablesWithoutVectorColumn()
    {
        using var context = CreateContext();

        context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Should().Contain(new[] { "bug_reports", "attachments", "idempotency_requests", "audit_events" });
        context.Model.GetEntityTypes()
            .Where(e => e.GetTableName() != "historical_tickets" && e.GetTableName() != "embedding_cache")
            .SelectMany(entity => entity.GetProperties())
            .Should().NotContain(property => property.GetColumnType() == "vector");
    }

    [Fact]
    public void PhaseThreeAndFourModel_ShouldEnforceJobAndEmbeddingIdentity()
    {
        using var context = CreateContext();
        var job = context.Model.FindEntityType(typeof(AnalysisJob));
        var ticket = context.Model.FindEntityType(typeof(HistoricalTicket));
        var cache = context.Model.FindEntityType(typeof(EmbeddingCacheEntry));

        job.Should().NotBeNull();
        job!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(AnalysisJob.QueueName),
                nameof(AnalysisJob.AnalysisRunId),
                nameof(AnalysisJob.ExpectedVersion)
            }));
        ticket.Should().NotBeNull();
        ticket!.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(HistoricalTicket.EmbeddingVersion),
                nameof(HistoricalTicket.EmbeddingDimension),
                nameof(HistoricalTicket.IndexedAt)
            }));
        cache.Should().NotBeNull();
        cache!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(EmbeddingCacheEntry.ContentHash),
                nameof(EmbeddingCacheEntry.Provider),
                nameof(EmbeddingCacheEntry.Model),
                nameof(EmbeddingCacheEntry.EmbeddingVersion),
                nameof(EmbeddingCacheEntry.Dimension)
            }));
    }
}
