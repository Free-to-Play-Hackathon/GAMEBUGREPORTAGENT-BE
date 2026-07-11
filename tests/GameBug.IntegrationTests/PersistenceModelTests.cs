using FluentAssertions;
using GameBug.Domain.BugReports;
using GameBug.Infrastructure.Persistence;
using GameBug.Domain.Analysis;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameBug.IntegrationTests;

public sealed class PersistenceModelTests
{
    private static GameBugDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GameBugDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test")
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
            .SelectMany(entity => entity.GetProperties())
            .Should().NotContain(property => property.GetColumnType() == "vector");
    }
}
