using FluentAssertions;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Duplicates;
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
        run.StartProcessing("sanitizer", "parser", "routingPolicy", DateTimeOffset.UtcNow);

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

    [Fact]
    public void DuplicateMatch_ShouldRejectInvalidRankAndScore()
    {
        var channelScores = new DuplicateChannelScores(1, 1, null, null, null, null, 0.1);
        var signalScores = new DuplicateScoreBreakdown(1, 0.8, null, null, 0.8, null, null);

        var badRank = DuplicateMatch.Create(
            Guid.NewGuid(),
            AnalysisRunId.CreateUnique(),
            Guid.NewGuid(),
            0,
            0.8,
            DuplicateClassification.RelatedIssue,
            channelScores,
            signalScores,
            new[] { "normalizedStackSignature" },
            Array.Empty<string>(),
            "Safe explanation",
            "hybrid-v1",
            null,
            null,
            "snapshot",
            DateTimeOffset.UtcNow);

        var badScore = DuplicateMatch.Create(
            Guid.NewGuid(),
            AnalysisRunId.CreateUnique(),
            Guid.NewGuid(),
            1,
            1.2,
            DuplicateClassification.RelatedIssue,
            channelScores,
            signalScores,
            new[] { "normalizedStackSignature" },
            Array.Empty<string>(),
            "Safe explanation",
            "hybrid-v1",
            null,
            null,
            "snapshot",
            DateTimeOffset.UtcNow);

        badRank.IsFailure.Should().BeTrue();
        badScore.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ScheduleRetry_ShouldReturnProcessingRunToQueuedWithoutLosingStage()
    {
        var run = AnalysisRun.Create(
            AnalysisRunId.CreateUnique(), BugReportId.CreateUnique(), 1, "input", "config", "schema").Value;
        run.Queue(DateTimeOffset.UtcNow);
        run.StartProcessing("sanitizer", "parser", "routing", DateTimeOffset.UtcNow);
        run.TransitionStage(AnalysisStage.ExtractingEvidence);

        var result = run.ScheduleRetry(
            "PROVIDER_TIMEOUT",
            new[] { new AnalysisWarning("PROVIDER_TIMEOUT", "Retryable provider failure.") },
            DateTimeOffset.UtcNow.AddSeconds(2),
            "TransientDependency");

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(AnalysisStatus.Queued);
        run.Stage.Should().Be(AnalysisStage.ExtractingEvidence);
        run.RetryCount.Should().Be(1);
        run.ErrorCode.Should().Be("PROVIDER_TIMEOUT");
        run.FailureCategory.Should().Be("TransientDependency");
        run.IsTerminal.Should().BeFalse();
    }
}
