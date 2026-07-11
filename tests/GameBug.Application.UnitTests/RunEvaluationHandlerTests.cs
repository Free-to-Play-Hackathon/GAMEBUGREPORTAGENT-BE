using FluentAssertions;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Application.BugReports.CreateReport;
using GameBug.Application.Evaluation;
using GameBug.Application.Evaluation.RunEvaluation;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Evaluation;
using GameBug.Domain.SharedKernel;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class RunEvaluationHandlerTests
{
    [Fact]
    public async Task Handle_ShouldFailWhenManifestIsNotAllowlisted()
    {
        var dependencies = CreateDependencies(manifest: null, useDefaultManifest: false);

        var result = await dependencies.Handler.Handle(new RunEvaluationCommand("unknown", "demo", "key"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await dependencies.Repository.DidNotReceive().AddAsync(Arg.Any<EvaluationRun>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistCasesInStableCaseIdOrder()
    {
        var dependencies = CreateDependencies(new EvaluationManifest(
            "demo-v1",
            "1.0",
            "1.0",
            "1.0",
            new[]
            {
                new EvaluationManifestCase("GB-HN-001", "test", "HardNegative"),
                new EvaluationManifestCase("GB-DUP-001", "test", "Duplicate")
            }));

        var result = await dependencies.Handler.Handle(new RunEvaluationCommand("demo-v1", "demo", "key"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dependencies.SavedRun.Should().NotBeNull();
        dependencies.SavedRun!.CaseResults.Select(c => c.CaseId)
            .Should().Equal("GB-DUP-001", "GB-HN-001");
    }

    [Fact]
    public async Task Handle_ShouldCompleteWithErrorsWhenOneCaseFails()
    {
        var dependencies = CreateDependencies();
        dependencies.FixtureLoader.LoadAsync("GB-DUP-001", Arg.Any<CancellationToken>())
            .Returns((EvaluationCaseFixture?)null);

        var result = await dependencies.Handler.Handle(new RunEvaluationCommand("demo-v1", "demo", "key"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dependencies.SavedRun!.Status.Should().Be(EvaluationRunStatus.CompletedWithErrors);
        dependencies.SavedRun.Metrics.Should().Contain(m => m.Name == "HardNegativeFpRate");
    }

    [Fact]
    public async Task Handle_ShouldMarkInvalidWhenComponentVersionIsMissing()
    {
        var dependencies = CreateDependencies(runtimeOptions: new EvaluationRuntimeOptions
        {
            SchemaVersion = "analysis-result-v1",
            SanitizerVersion = "sanitizer-v1",
            ParserVersion = "parser-v1",
            RoutingPolicyVersion = "routing-v1",
            RankerVersion = "hybrid-v1",
            TrustPolicyVersion = "trust-policy-v1",
            PerCaseTimeoutSeconds = 1
        });

        var result = await dependencies.Handler.Handle(new RunEvaluationCommand("demo-v1", "demo", "key"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        dependencies.SavedRun!.Validity.Should().Be(EvaluationValidity.InvalidForClaim);
        dependencies.SavedRun.InvalidReason.Should().Be(nameof(InvalidReasonCode.MissingComponentVersion));
    }

    private static TestDependencies CreateDependencies(
        EvaluationManifest? manifest = null,
        bool useDefaultManifest = true,
        EvaluationRuntimeOptions? runtimeOptions = null)
    {
        if (useDefaultManifest && manifest is null)
        {
            manifest = new EvaluationManifest(
                "demo-v1",
                "1.0",
                "1.0",
                "1.0",
                new[]
                {
                    new EvaluationManifestCase("GB-DUP-001", "test", "Duplicate"),
                    new EvaluationManifestCase("GB-HN-001", "test", "HardNegative")
                });
        }

        var manifestLoader = Substitute.For<IEvaluationManifestLoader>();
        manifestLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(manifest);

        var groundTruthLoader = Substitute.For<IEvaluationGroundTruthLoader>();
        groundTruthLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EvaluationGroundTruth("1.0", new[]
            {
                new EvaluationGroundTruthEntry("GB-DUP-001", "BUG-201", 1)
            }));

        var fixtureLoader = Substitute.For<IEvaluationCaseFixtureLoader>();
        fixtureLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new EvaluationCaseFixture(
                call.ArgAt<string>(0),
                "Game crashes after opening inventory.",
                "1.2.3",
                "Windows",
                "PC",
                "en-US",
                "eval-session",
                "NullReferenceException"));

        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CreateReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreateReportResult(Guid.NewGuid(), "Submitted", 1, DateTimeOffset.UtcNow)));
        sender.Send(Arg.Any<StartAnalysisCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success(new StartAnalysisResult(
                Guid.NewGuid(),
                call.ArgAt<StartAnalysisCommand>(0).ReportId,
                1,
                "queued",
                "/status",
                "/result")));

        var analysisRuns = Substitute.For<IAnalysisRunRepository>();
        analysisRuns.GetAsync(Arg.Any<AnalysisRunId>(), Arg.Any<CancellationToken>())
            .Returns(call => CreateCompletedRun(call.ArgAt<AnalysisRunId>(0)));
        analysisRuns.GetDuplicateMatchesAsync(Arg.Any<AnalysisRunId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GameBug.Domain.Duplicates.DuplicateMatch>());

        var historicalTickets = Substitute.For<IHistoricalTicketRepository>();

        EvaluationRun? savedRun = null;
        var repository = Substitute.For<IEvaluationRunRepository>();
        repository.AddAsync(Arg.Do<EvaluationRun>(run => savedRun = run), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        runtimeOptions ??= new EvaluationRuntimeOptions
        {
            SchemaVersion = "analysis-result-v1",
            SanitizerVersion = "sanitizer-v1",
            ParserVersion = "parser-v1",
            RoutingPolicyVersion = "routing-v1",
            EmbeddingVersion = "embedding-v1",
            RankerVersion = "hybrid-v1",
            TrustPolicyVersion = "trust-policy-v1",
            PerCaseTimeoutSeconds = 1
        };

        var handler = new RunEvaluationCommandHandler(
            manifestLoader,
            groundTruthLoader,
            fixtureLoader,
            repository,
            analysisRuns,
            historicalTickets,
            unitOfWork,
            sender,
            new EvaluationIdentityBuilder(),
            new DuplicateMetricCalculator(),
            new LatencyMetricCalculator(),
            Options.Create(runtimeOptions));

        return new TestDependencies(handler, repository, fixtureLoader, () => savedRun);
    }

    private static AnalysisRun CreateCompletedRun(AnalysisRunId runId)
    {
        var run = AnalysisRun.Create(
            runId,
            BugReportId.CreateUnique(),
            1,
            "input",
            "config",
            "analysis-result-v1").Value;
        run.Queue(DateTimeOffset.UtcNow);
        run.StartProcessing("sanitizer-v1", "parser-v1", "routing-v1", DateTimeOffset.UtcNow);
        run.TransitionStage(AnalysisStage.ExtractingEvidence);
        run.TransitionStage(AnalysisStage.ExtractingVisualEvidence);
        run.TransitionStage(AnalysisStage.GroundingGameContext);
        run.TransitionStage(AnalysisStage.GeneratingRepro);
        run.TransitionStage(AnalysisStage.SearchingDuplicates);
        run.TransitionStage(AnalysisStage.PersistingResult);
        run.AwaitQaReview("analysis-results/test", Array.Empty<AnalysisWarning>(), DateTimeOffset.UtcNow);
        return run;
    }

    private sealed class TestDependencies
    {
        private readonly Func<EvaluationRun?> _savedRunAccessor;

        public TestDependencies(
            RunEvaluationCommandHandler handler,
            IEvaluationRunRepository repository,
            IEvaluationCaseFixtureLoader fixtureLoader,
            Func<EvaluationRun?> savedRunAccessor)
        {
            Handler = handler;
            Repository = repository;
            FixtureLoader = fixtureLoader;
            _savedRunAccessor = savedRunAccessor;
        }

        public RunEvaluationCommandHandler Handler { get; }
        public IEvaluationRunRepository Repository { get; }
        public IEvaluationCaseFixtureLoader FixtureLoader { get; }
        public EvaluationRun? SavedRun => _savedRunAccessor();
    }
}
