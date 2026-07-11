using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Jobs;
using FluentAssertions;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using NSubstitute;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class StartAnalysisHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReplaySameAnalysisIdentity()
    {
        var reports = Substitute.For<IBugReportRepository>();
        var runs = Substitute.For<IAnalysisRunRepository>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        var outbox = Substitute.For<IAnalysisOutboxStore>();
        var aiTaskRouter = Substitute.For<IAiTaskRouter>();
        aiTaskRouter.Resolve(Arg.Any<AiTask>(), Arg.Any<AiRoutingContext>())
            .Returns(new AiRoute("default", "provider", "model", "prompt", "schema", "policy", 30, 2048));

        var reportId = BugReportId.CreateUnique();
        var report = BugReport.Submit(
            reportId, "A sufficiently long description", "1.0", "Windows", null, null, null,
            "owner", DateTimeOffset.UtcNow).Value;
        reports.GetAsync(reportId, Arg.Any<CancellationToken>()).Returns(report);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns("owner");
        idempotency.TryAddAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>()).Returns(true, false);

        IdempotencyRecord? completed = null;
        AnalysisRun? createdRun = null;
        idempotency.UpdateAsync(Arg.Do<IdempotencyRecord>(record => completed = record), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        runs.AddAsync(Arg.Do<AnalysisRun>(run => createdRun = run), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        idempotency.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => completed);
        runs.GetAsync(Arg.Any<AnalysisRunId>(), Arg.Any<CancellationToken>()).Returns(_ => createdRun);
        var handler = new StartAnalysisCommandHandler(reports, runs, idempotency, unitOfWork, currentUser, aiTaskRouter, outbox);
        var command = new StartAnalysisCommand(
            reportId.Value, "1234567890123456", "analysis-result-v1", "default");

        var first = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.AnalysisId.Should().Be(first.Value.AnalysisId);
        createdRun!.Status.Should().Be(AnalysisStatus.Queued);
        await outbox.Received(1).AddProcessAnalysisMessageAsync(createdRun.Id, createdRun.Version, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldConcealReportOwnedByAnotherUser()
    {
        var reports = Substitute.For<IBugReportRepository>();
        var runs = Substitute.For<IAnalysisRunRepository>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        var outbox = Substitute.For<IAnalysisOutboxStore>();
        var aiTaskRouter = Substitute.For<IAiTaskRouter>();
        aiTaskRouter.Resolve(Arg.Any<AiTask>(), Arg.Any<AiRoutingContext>())
            .Returns(new AiRoute("default", "provider", "model", "prompt", "schema", "policy", 30, 2048));

        var reportId = BugReportId.CreateUnique();
        reports.GetAsync(reportId, Arg.Any<CancellationToken>()).Returns(BugReport.Submit(
            reportId, "A sufficiently long description", null, null, null, null, null,
            "owner", DateTimeOffset.UtcNow).Value);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns("intruder");
        var handler = new StartAnalysisCommandHandler(reports, runs, idempotency, unitOfWork, currentUser, aiTaskRouter, outbox);

        var result = await handler.Handle(new StartAnalysisCommand(
            reportId.Value, "1234567890123456", "analysis-result-v1", "default"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BugReport.NotFound");
    }
}
