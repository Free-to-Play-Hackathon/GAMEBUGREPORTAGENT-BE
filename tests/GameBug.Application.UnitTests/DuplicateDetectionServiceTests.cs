using FluentAssertions;
using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Duplicates;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class DuplicateDetectionServiceTests
{
    [Fact]
    public async Task DetectAsync_ShouldRankGroundedDuplicateFirst()
    {
        var repository = Substitute.For<IHistoricalTicketRepository>();
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        var fixture = CreateFixture("Game crashes after opening inventory", "stack-a");
        var candidate = CreateTicket(
            "BUG-201",
            "Inventory crash",
            "Game crashes after opening inventory",
            "stack-a",
            "open inventory",
            "inventory",
            "Game crashes after opening inventory");
        candidate.SetEmbedding(new[] { 1f, 0f }, "test", "test-model", "embedding-v1", 2, DateTimeOffset.UtcNow);

        ConfigureRepository(repository, candidate);
        embeddingProvider.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult(new[] { 1f, 0f }, "test", "test-model", "embedding-v1", 2));

        var service = new DuplicateDetectionService(
            repository,
            embeddingProvider,
            Options.Create(new DuplicateDetectionOptions()));

        var result = await service.DetectAsync(
            fixture.Run,
            fixture.ReproCase,
            fixture.EvidencePack,
            CancellationToken.None);

        result.Matches.Should().ContainSingle();
        result.Matches[0].HistoricalTicketId.Should().Be(candidate.Id);
        result.Matches[0].Rank.Should().Be(1);
        result.Matches[0].Classification.Should().Be(DuplicateClassification.LikelyDuplicate);
        result.Matches[0].FinalScore.Should().BeGreaterThanOrEqualTo(0.82);
        await repository.Received(1).SaveDuplicateMatchesAsync(
            fixture.Run.Id,
            Arg.Is<IReadOnlyCollection<DuplicateMatch>>(items => items.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAsync_ShouldHardCapSameWordingWithConflictingStackAndOutcome()
    {
        var repository = Substitute.For<IHistoricalTicketRepository>();
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        var fixture = CreateFixture("Game crashes after opening inventory", "stack-a");
        var hardNegative = CreateTicket(
            "BUG-NEGATIVE",
            "Inventory issue",
            "Game crashes after opening inventory",
            "stack-b",
            "open inventory",
            "inventory",
            "Reward is missing after purchase");
        hardNegative.SetEmbedding(new[] { 1f, 0f }, "test", "test-model", "embedding-v1", 2, DateTimeOffset.UtcNow);

        ConfigureRepository(repository, hardNegative, includeExact: false);
        embeddingProvider.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult(new[] { 1f, 0f }, "test", "test-model", "embedding-v1", 2));

        var service = new DuplicateDetectionService(
            repository,
            embeddingProvider,
            Options.Create(new DuplicateDetectionOptions()));

        var result = await service.DetectAsync(
            fixture.Run,
            fixture.ReproCase,
            fixture.EvidencePack,
            CancellationToken.None);

        result.Matches.Should().ContainSingle();
        result.Matches[0].Classification.Should().NotBe(DuplicateClassification.LikelyDuplicate);
        result.Matches[0].FinalScore.Should().BeLessThanOrEqualTo(0.81);
        result.Matches[0].ConflictingSignals.Should().Contain("normalizedStackSignature");
        result.Matches[0].ConflictingSignals.Should().Contain("actualResult");
    }

    private static void ConfigureRepository(
        IHistoricalTicketRepository repository,
        HistoricalTicket ticket,
        bool includeExact = true)
    {
        repository.GetIndexSnapshotVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("index-v1");
        repository.GetExactCandidatesAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(includeExact ? new[] { ticket } : Array.Empty<HistoricalTicket>());
        repository.GetLexicalCandidatesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { ticket });
        repository.GetVectorCandidatesAsync(
                Arg.Any<Guid>(),
                Arg.Any<float[]>(),
                "embedding-v1",
                2,
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { ticket });
    }

    private static (AnalysisRun Run, ReproCase ReproCase, EvidencePack EvidencePack) CreateFixture(
        string actualResult,
        string stackSignature)
    {
        var run = AnalysisRun.Create(
            AnalysisRunId.CreateUnique(),
            BugReportId.CreateUnique(),
            1,
            "input-hash",
            "config-hash",
            "analysis-result-v1").Value;
        var reproCase = ReproCase.Create(
            Guid.NewGuid(),
            run.Id,
            "Inventory crash",
            "1.2.3",
            "Windows",
            "A saved game is loaded",
            new[]
            {
                new ReproStep(
                    Guid.NewGuid(),
                    1,
                    "Open inventory",
                    StepType.SuggestedToVerify,
                    null,
                    "Reported trigger action")
            },
            "Inventory opens",
            actualResult,
            Severity.High,
            "The current session is blocked.",
            null,
            ConfidenceScore.Create(0.9).Value).Value;
        var source = new EvidenceSource(
            EvidenceSourceType.Log,
            "attachment-1",
            1,
            1,
            stackSignature,
            "hash",
            TrustLevel.Observed);
        var stackFact = EvidenceFact.Create(
            Guid.NewGuid(),
            "stackSignature",
            stackSignature,
            EvidenceStatus.Supported,
            1,
            new[] { source }).Value;
        var evidencePack = new EvidencePack(
            Guid.NewGuid(),
            run.Id,
            new[] { stackFact },
            Array.Empty<EventTimelineEntry>());

        return (run, reproCase, evidencePack);
    }

    private static HistoricalTicket CreateTicket(
        string externalId,
        string title,
        string summary,
        string stackSignature,
        string trigger,
        string scene,
        string actualResult)
    {
        string searchText = DuplicateTextNormalizer.BuildSearchText(
            DuplicateSearchDocumentBuilder.TemplateVersion,
            title,
            summary,
            trigger,
            scene,
            actualResult,
            stackSignature,
            "Windows",
            "1.2.3");
        return HistoricalTicket.Create(
            Guid.NewGuid(),
            DuplicateSearchDocumentBuilder.DefaultProjectId,
            "test",
            externalId,
            title,
            summary,
            "open",
            "high",
            "1.0.0",
            "2.0.0",
            new[] { "Windows" },
            stackSignature,
            stackSignature,
            new[] { "inventory" },
            summary,
            trigger,
            scene,
            actualResult,
            searchText,
            DuplicateTextNormalizer.Hash(searchText),
            "import-v1",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow).Value;
    }
}
