using FluentAssertions;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Analysis.GetAnalysis;
using GameBug.Application.Analysis.GetAnalysisResult;
using GameBug.Application.Duplicates;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class AnalysisQuerySecurityTests
{
    [Fact]
    public async Task GetResult_ShouldReturnNotReadyBeforeCompletion()
    {
        var runs = Substitute.For<IAnalysisRunRepository>();
        var historicalTickets = Substitute.For<IHistoricalTicketRepository>();
        var trustReports = Substitute.For<ITrustReportRepository>();
        var reports = Substitute.For<IBugReportRepository>();
        var user = Substitute.For<ICurrentUser>();
        var reportId = BugReportId.CreateUnique();
        var run = AnalysisRun.Create(AnalysisRunId.CreateUnique(), reportId, 1, "input", "config", "schema").Value;
        var report = BugReport.Submit(reportId, "A sufficiently long description", null, null, null, null, null, "owner", DateTimeOffset.UtcNow).Value;
        runs.GetAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        reports.GetAsync(reportId, Arg.Any<CancellationToken>()).Returns(report);
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("owner");
        var handler = new GetAnalysisResultQueryHandler(
            runs,
            historicalTickets,
            trustReports,
            reports,
            user,
            Options.Create(new EmbeddingOptions()),
            Options.Create(new DuplicateDetectionOptions()));

        var result = await handler.Handle(new GetAnalysisResultQuery(run.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Analysis.ResultNotReady");
        await runs.DidNotReceiveWithAnyArgs().GetEvidencePackAsync(default!, default);
    }

    [Fact]
    public async Task GetStatus_ShouldConcealAnotherUsersRun()
    {
        var runs = Substitute.For<IAnalysisRunRepository>();
        var historicalTickets = Substitute.For<IHistoricalTicketRepository>();
        var trustReports = Substitute.For<ITrustReportRepository>();
        var reports = Substitute.For<IBugReportRepository>();
        var user = Substitute.For<ICurrentUser>();
        var reportId = BugReportId.CreateUnique();
        var run = AnalysisRun.Create(AnalysisRunId.CreateUnique(), reportId, 1, "input", "config", "schema").Value;
        var report = BugReport.Submit(reportId, "A sufficiently long description", null, null, null, null, null, "owner", DateTimeOffset.UtcNow).Value;
        runs.GetAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        reports.GetAsync(reportId, Arg.Any<CancellationToken>()).Returns(report);
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("intruder");
        var handler = new GetAnalysisQueryHandler(runs, reports, user);

        var result = await handler.Handle(new GetAnalysisQuery(run.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Analysis.NotFound");
    }

    [Fact]
    public async Task GetStatus_ShouldUseLowerCamelAndNullStageForCompletedRun()
    {
        var runs = Substitute.For<IAnalysisRunRepository>();
        var historicalTickets = Substitute.For<IHistoricalTicketRepository>();
        var trustReports = Substitute.For<ITrustReportRepository>();
        var reports = Substitute.For<IBugReportRepository>();
        var user = Substitute.For<ICurrentUser>();
        var reportId = BugReportId.CreateUnique();
        var run = AnalysisRun.Create(AnalysisRunId.CreateUnique(), reportId, 1, "input", "config", "schema").Value;
        run.StartProcessing("sanitizer", "parser", "routing", DateTimeOffset.UtcNow);
        run.TransitionStage(AnalysisStage.ExtractingEvidence);
        run.TransitionStage(AnalysisStage.GroundingGameContext);
        run.TransitionStage(AnalysisStage.GeneratingRepro);
        run.TransitionStage(AnalysisStage.PersistingResult);
        run.Complete("result", Array.Empty<AnalysisWarning>(), DateTimeOffset.UtcNow);
        var report = BugReport.Submit(reportId, "A sufficiently long description", null, null, null, null, null, "owner", DateTimeOffset.UtcNow).Value;
        runs.GetAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        reports.GetAsync(reportId, Arg.Any<CancellationToken>()).Returns(report);
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("owner");

        var result = await new GetAnalysisQueryHandler(runs, reports, user)
            .Handle(new GetAnalysisQuery(run.Id.Value), CancellationToken.None);

        result.Value.Status.Should().Be("completed");
        result.Value.Stage.Should().BeNull();
    }

    [Fact]
    public async Task GetResult_ShouldReturnAnalysisFailedForFailedRun()
    {
        var runs = Substitute.For<IAnalysisRunRepository>();
        var historicalTickets = Substitute.For<IHistoricalTicketRepository>();
        var trustReports = Substitute.For<ITrustReportRepository>();
        var reports = Substitute.For<IBugReportRepository>();
        var user = Substitute.For<ICurrentUser>();
        var reportId = BugReportId.CreateUnique();
        var run = AnalysisRun.Create(AnalysisRunId.CreateUnique(), reportId, 1, "input", "config", "schema").Value;
        run.Fail("PROVIDER_TIMEOUT", Array.Empty<AnalysisWarning>(), DateTimeOffset.UtcNow);
        var report = BugReport.Submit(reportId, "A sufficiently long description", null, null, null, null, null, "owner", DateTimeOffset.UtcNow).Value;
        runs.GetAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        reports.GetAsync(reportId, Arg.Any<CancellationToken>()).Returns(report);
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("owner");

        var result = await new GetAnalysisResultQueryHandler(
                runs,
                historicalTickets,
                trustReports,
                reports,
                user,
                Options.Create(new EmbeddingOptions()),
                Options.Create(new DuplicateDetectionOptions()))
            .Handle(new GetAnalysisResultQuery(run.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Analysis.Failed");
    }
}
