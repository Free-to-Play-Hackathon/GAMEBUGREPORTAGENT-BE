using FluentAssertions;
using GameBug.Application.Abstractions.Files;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Time;
using GameBug.Application.BugReports.CreateReport;
using GameBug.Domain.BugReports;
using NSubstitute;
using Xunit;

namespace GameBug.Application.UnitTests;

public class CreateReportHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IObjectStorage _objectStorage = Substitute.For<IObjectStorage>();
    private readonly IBugReportRepository _bugReportRepository = Substitute.For<IBugReportRepository>();
    private readonly IIdempotencyStore _idempotencyStore = Substitute.For<IIdempotencyStore>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditWriter _auditWriter = Substitute.For<IAuditWriter>();
    private readonly CreateReportHandler _handler;

    public CreateReportHandlerTests()
    {
        _handler = new CreateReportHandler(
            _currentUser,
            _clock,
            _objectStorage,
            _bugReportRepository,
            _idempotencyStore,
            _unitOfWork,
            _auditWriter);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIsNotAuthenticated()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(false);
        var command = new CreateReportCommand("Valid description details...", null, null, null, null, null, "key1234567890123456", new List<FileAttachmentCommand>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenKeyHasDifferentPayload()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("User1");

        var record = new IdempotencyRecord(
            "User1:key1234567890123456",
            "some-hash",
            IdempotencyStatus.Processing,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));

        _idempotencyStore.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(record);

        var command = new CreateReportCommand("Valid description details...", null, null, null, null, null, "key1234567890123456", new List<FileAttachmentCommand>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Idempotency.Conflict");
    }

    [Fact]
    public async Task Handle_ShouldReplaySameTextOnlyRequest()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("User1");
        _clock.UtcNow.Returns(DateTimeOffset.Parse("2026-07-11T00:00:00Z"));

        IdempotencyRecord? completed = null;
        BugReport? savedReport = null;
        _idempotencyStore.TryAddAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>())
            .Returns(true, false);
        _idempotencyStore.UpdateAsync(Arg.Do<IdempotencyRecord>(record => completed = record), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _idempotencyStore.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => completed);
        _bugReportRepository.AddAsync(Arg.Do<BugReport>(report => savedReport = report), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _bugReportRepository.GetAsync(Arg.Any<BugReportId>(), Arg.Any<CancellationToken>())
            .Returns(_ => savedReport);

        var command = new CreateReportCommand(
            "Valid description details...", null, null, null, null, null,
            "key1234567890123456", Array.Empty<FileAttachmentCommand>());

        var first = await _handler.Handle(command, CancellationToken.None);
        var replay = await _handler.Handle(command, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.ReportId.Should().Be(first.Value.ReportId);
        await _objectStorage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }
}
