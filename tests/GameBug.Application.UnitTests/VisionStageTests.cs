using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Files;
using GameBug.Application.Abstractions.Parsing;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Trust;
using GameBug.Application.Abstractions.Vision;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Application.Duplicates;
using GameBug.Application.Evidence;
using GameBug.Application.ReproCases;
using GameBug.Application.Trust;
using GameBug.Application.Vision;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evidence;
using GameBug.Domain.GameContext;
using GameBug.Domain.ReproCases;
using GameBug.Domain.Trust;
using GameBug.Infrastructure.AI;
using GameBug.Infrastructure.Parsing;
using GameBug.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class VisionStageTests
{
    [Fact]
    public async Task Disabled_ShouldSkipCheckpointAndNotCallExtractor()
    {
        var fixture = CreateFixture(new VisionOptions());
        var visualCheckpoint = CaptureVisualCheckpoint(fixture);
        var trustReport = CaptureTrustReport(fixture);

        var result = await fixture.Handler.Handle(new ProcessAnalysisCommand(fixture.Run.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error.Description);
        fixture.Run.Status.Should().Be(AnalysisStatus.AwaitingQaReview);
        fixture.Run.Warnings.Should().Contain(w => w.Code == "VISION_DISABLED");
        visualCheckpoint.Value.Should().NotBeNull();
        ReadOutcome(visualCheckpoint.Value!).Should().Be("Skipped");
        ReadWarningCodes(visualCheckpoint.Value!).Should().Contain("VISION_DISABLED");
        await fixture.VisualExtractor.DidNotReceiveWithAnyArgs().ExtractAsync(default!, default);
        trustReport.Value.Should().NotBeNull();
        trustReport.Value!.Violations.Should().Contain(v =>
            v.Code == "VISION_DISABLED" &&
            v.OutputPath == "analysisRun.stages.extractingVisualEvidence" &&
            !v.IsBlocking);
    }

    [Fact]
    public async Task EnabledWithoutScreenshot_ShouldSkipCheckpointAndNotCallExtractor()
    {
        var fixture = CreateFixture(new VisionOptions { Enabled = true, Provider = "Unavailable" });
        var visualCheckpoint = CaptureVisualCheckpoint(fixture);

        var result = await fixture.Handler.Handle(new ProcessAnalysisCommand(fixture.Run.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error.Description);
        fixture.Run.Status.Should().Be(AnalysisStatus.AwaitingQaReview);
        fixture.Run.Warnings.Should().Contain(w => w.Code == "VISION_NO_SCREENSHOT");
        visualCheckpoint.Value.Should().NotBeNull();
        ReadOutcome(visualCheckpoint.Value!).Should().Be("Skipped");
        ReadWarningCodes(visualCheckpoint.Value!).Should().Contain("VISION_NO_SCREENSHOT");
        await fixture.VisualExtractor.DidNotReceiveWithAnyArgs().ExtractAsync(default!, default);
    }

    [Fact]
    public async Task ProviderUnavailable_ShouldDegradeAndContinuePipeline()
    {
        var options = new VisionOptions { Enabled = true, Provider = "Unavailable" };
        var report = CreateReport(withScreenshot: true);
        var fixture = CreateFixture(options, report);
        var visualCheckpoint = CaptureVisualCheckpoint(fixture);
        fixture.VisualExtractor.ExtractAsync(Arg.Any<VisualExtractionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VisualExtractionResult(
                VisionStageOutcome.Degraded,
                Array.Empty<EvidenceFact>(),
                new[] { new AnalysisWarning("VISION_PROVIDER_UNAVAILABLE", "Visual evidence extraction is unavailable.") },
                options.Provider,
                options.StageVersion,
                0));

        var result = await fixture.Handler.Handle(new ProcessAnalysisCommand(fixture.Run.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error.Description);
        fixture.Run.Status.Should().Be(AnalysisStatus.AwaitingQaReview);
        fixture.Run.Warnings.Should().Contain(w => w.Code == "VISION_PROVIDER_UNAVAILABLE");
        visualCheckpoint.Value.Should().NotBeNull();
        ReadOutcome(visualCheckpoint.Value!).Should().Be("Degraded");
        await fixture.VisualExtractor.Received(1).ExtractAsync(Arg.Any<VisualExtractionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderException_ShouldDegradeAndNotFailAnalysis()
    {
        var options = new VisionOptions { Enabled = true, Provider = "Unavailable" };
        var report = CreateReport(withScreenshot: true);
        var fixture = CreateFixture(options, report);
        var visualCheckpoint = CaptureVisualCheckpoint(fixture);
        fixture.VisualExtractor.ExtractAsync(Arg.Any<VisualExtractionRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<VisualExtractionResult>>(_ => throw new InvalidOperationException("provider offline"));

        var result = await fixture.Handler.Handle(new ProcessAnalysisCommand(fixture.Run.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error.Description);
        fixture.Run.Status.Should().Be(AnalysisStatus.AwaitingQaReview);
        fixture.Run.ErrorCode.Should().BeNull();
        visualCheckpoint.Value.Should().NotBeNull();
        ReadOutcome(visualCheckpoint.Value!).Should().Be("Degraded");
        ReadWarningCodes(visualCheckpoint.Value!).Should().Contain("VISION_PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task CheckpointRestore_ShouldNotProcessScreenshotAgain()
    {
        var options = new VisionOptions { Enabled = true, Provider = "Unavailable" };
        var report = CreateReport(withScreenshot: true);
        var fixture = CreateFixture(options, report);
        var visualCheckpoint = new AnalysisCheckpoint(
            Guid.NewGuid(),
            fixture.Run.Id,
            AnalysisStage.ExtractingVisualEvidence,
            options.StageVersion,
            VisualInputHash(report, options),
            "Started",
            1,
            DateTimeOffset.UtcNow);
        visualCheckpoint.Complete(
            JsonSerializer.Serialize(new
            {
                Outcome = "Degraded",
                EvidenceFactIds = Array.Empty<Guid>(),
                Provider = options.Provider,
                StageVersion = options.StageVersion,
                ProcessedAttachmentCount = 0
            }),
            JsonSerializer.Serialize(new[] { new AnalysisWarning("VISION_PROVIDER_UNAVAILABLE", "Visual evidence extraction is unavailable.") }),
            DateTimeOffset.UtcNow);
        fixture.RunRepository.GetCheckpointAsync(
                fixture.Run.Id,
                AnalysisStage.ExtractingVisualEvidence,
                options.StageVersion,
                VisualInputHash(report, options),
                Arg.Any<CancellationToken>())
            .Returns(visualCheckpoint);

        var result = await fixture.Handler.Handle(new ProcessAnalysisCommand(fixture.Run.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error.Description);
        fixture.Run.Status.Should().Be(AnalysisStatus.AwaitingQaReview);
        await fixture.VisualExtractor.DidNotReceiveWithAnyArgs().ExtractAsync(default!, default);
        await fixture.StorageReader.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default, default!, default);
    }

    private static Capture<AnalysisCheckpoint> CaptureVisualCheckpoint(HandlerFixture fixture)
    {
        var captured = new Capture<AnalysisCheckpoint>();
        fixture.RunRepository.SaveCheckpointAsync(
                Arg.Do<AnalysisCheckpoint>(checkpoint =>
                {
                    if (checkpoint.Stage == AnalysisStage.ExtractingVisualEvidence)
                    {
                        captured.Value = checkpoint;
                    }
                }),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return captured;
    }

    private static Capture<TrustReport> CaptureTrustReport(HandlerFixture fixture)
    {
        var captured = new Capture<TrustReport>();
        fixture.TrustReportRepository.AddAsync(
                Arg.Do<TrustReport>(report => captured.Value = report),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return captured;
    }

    private static string ReadOutcome(AnalysisCheckpoint checkpoint)
    {
        using var document = JsonDocument.Parse(checkpoint.OutputReference!);
        return document.RootElement.GetProperty("Outcome").GetString()!;
    }

    private static IReadOnlyCollection<string> ReadWarningCodes(AnalysisCheckpoint checkpoint)
    {
        var warnings = JsonSerializer.Deserialize<List<AnalysisWarning>>(checkpoint.WarningCodesJson!);
        return warnings?.Select(warning => warning.Code).ToList() ?? new List<string>();
    }

    private static HandlerFixture CreateFixture(VisionOptions visionOptions, BugReport? report = null)
    {
        var runRepository = Substitute.For<IAnalysisRunRepository>();
        var reportRepository = Substitute.For<IBugReportRepository>();
        var storageReader = Substitute.For<IObjectStorageReader>();
        var contextRepository = Substitute.For<IGameContextRepository>();
        var aiGateway = Substitute.For<IStructuredAiGateway>();
        var aiRouter = Substitute.For<IAiTaskRouter>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var duplicateDetection = Substitute.For<IDuplicateDetectionService>();
        var visualExtractor = Substitute.For<IVisualEvidenceExtractor>();
        var trustReportRepository = Substitute.For<ITrustReportRepository>();

        report ??= CreateReport(withScreenshot: false);
        reportRepository.GetAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        var run = AnalysisRun.Create(AnalysisRunId.CreateUnique(), report.Id, 1, "input-hash", "config-hash", "1.0.0").Value;
        runRepository.GetAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        contextRepository.GetGameEntitiesAsync(Arg.Any<CancellationToken>()).Returns(new List<GameEntity>());
        contextRepository.GetExpectedBehaviorsAsync(Arg.Any<CancellationToken>()).Returns(new List<ExpectedBehavior>());
        aiRouter.Resolve(Arg.Any<AiTask>(), Arg.Any<AiRoutingContext>())
            .Returns(call => new AiRoute(
                call.Arg<AiTask>() == AiTask.NormalizeBugReport ? "report-understanding" : "repro-synthesis",
                "test-provider",
                "test-model",
                "v1",
                "analysis-result-v1",
                "routing-v1",
                30,
                4096));
        aiGateway.GenerateStructuredResponseAsync(
                AiTask.NormalizeBugReport,
                Arg.Any<AiRoute>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new AiGenerationResult(
                "{\"symptom\":\"crash\",\"action\":\"open store\",\"context\":\"Android\",\"missingInformation\":[]}",
                "test-provider",
                "test-model",
                "test-model",
                1));
        aiGateway.GenerateStructuredResponseAsync(
                AiTask.SynthesizeReproCase,
                Arg.Any<AiRoute>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new AiGenerationResult(SuggestedReproJson, "test-provider", "test-model", "test-model", 1));
        duplicateDetection.DetectAsync(Arg.Any<AnalysisRun>(), Arg.Any<ReproCase>(), Arg.Any<EvidencePack>(), Arg.Any<CancellationToken>())
            .Returns(new DuplicateDetectionResult(
                Array.Empty<DuplicateMatch>(),
                "input",
                "empty-index",
                "hash-embedding",
                "embedding-v1",
                "hybrid-v1"));

        var handler = new ProcessAnalysisCommandHandler(
            runRepository,
            reportRepository,
            storageReader,
            new ContentSanitizer(),
            new GenericCrashLogParser(),
            new EvidenceResolver(),
            new EventTimelineBuilder(),
            contextRepository,
            aiGateway,
            aiRouter,
            new PromptLoader(),
            new ReproValidator(new SeverityPolicy()),
            duplicateDetection,
            unitOfWork,
            new MvpProvenanceValidator(),
            new MvpQualityGate(),
            trustReportRepository,
            visualExtractor,
            Options.Create(visionOptions),
            NullLogger<ProcessAnalysisCommandHandler>.Instance);

        return new HandlerFixture(
            handler,
            run,
            runRepository,
            storageReader,
            visualExtractor,
            trustReportRepository);
    }

    private static BugReport CreateReport(bool withScreenshot)
    {
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Game crashes on opening buy store page.",
            "1.0.0",
            "Android",
            "Pixel 6",
            "en-US",
            "SessionRef123",
            "User123",
            DateTimeOffset.UtcNow).Value;

        if (withScreenshot)
        {
            report.AddAttachment(
                AttachmentId.CreateUnique(),
                "opaque/screenshot-object",
                "screen.png",
                AttachmentType.Screenshot,
                "image/png",
                128,
                "screenshot-sha256",
                DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        }

        return report;
    }

    private static string VisualInputHash(BugReport report, VisionOptions options)
    {
        string screenshotAttachmentsHash = string.Join('|', report.Attachments
            .Where(a => a.AttachmentType == AttachmentType.Screenshot)
            .OrderBy(a => a.Id.Value)
            .Take(options.MaxImagesPerAnalysis)
            .Select(a => $"{a.Id.Value}:{a.Checksum}"));
        return Hash($"{screenshotAttachmentsHash}|{options.Enabled}|{options.Provider}|{options.StageVersion}");
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private const string SuggestedReproJson = """
        {
            "title": "Android crash on opening Store",
            "buildVersion": "1.0.0",
            "platform": "Android",
            "preconditions": "User is logged in",
            "steps": [
                { "order": 1, "description": "Open the buy store page", "stepType": "SuggestedToVerify", "sourceId": null, "inferenceReason": "Inferred from the player report" }
            ],
            "expectedResult": "Store page opens smoothly",
            "actualResult": "Application crashes",
            "severityEstimate": "High",
            "severityReason": "Causes crash",
            "missingInformation": null,
            "confidence": 0.9
        }
        """;

    private sealed record HandlerFixture(
        ProcessAnalysisCommandHandler Handler,
        AnalysisRun Run,
        IAnalysisRunRepository RunRepository,
        IObjectStorageReader StorageReader,
        IVisualEvidenceExtractor VisualExtractor,
        ITrustReportRepository TrustReportRepository);

    private sealed class Capture<T>
    {
        public T? Value { get; set; }
    }
}
