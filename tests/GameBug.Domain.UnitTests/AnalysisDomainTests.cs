using FluentAssertions;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Evidence;
using Xunit;

namespace GameBug.Domain.UnitTests;

public sealed class AnalysisDomainTests
{
    [Fact]
    public void Complete_ShouldRequirePersistingStageAndResultReference()
    {
        var run = AnalysisRun.Create(
            AnalysisRunId.CreateUnique(), BugReportId.CreateUnique(), 1, "input", "config", "schema").Value;
        run.StartProcessing("sanitizer", "parser", "prompt", "provider", "model", DateTimeOffset.UtcNow);

        var result = run.Complete("", Array.Empty<AnalysisWarning>(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConflictEvidence_ShouldRequireIndependentSources()
    {
        var source = new EvidenceSource(EvidenceSourceType.Log, "attachment-1", 1, 1, "safe", "hash", TrustLevel.Observed);

        var result = EvidenceFact.Create(
            Guid.NewGuid(), "platform", "conflict", EvidenceStatus.Conflict, 0.5, new[] { source });

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UnknownEvidence_ShouldNotContainAValue()
    {
        var result = EvidenceFact.Create(
            Guid.NewGuid(), "buildVersion", "Unknown", EvidenceStatus.Unknown, 0, Array.Empty<EvidenceSource>());

        result.IsFailure.Should().BeTrue();
    }
}
