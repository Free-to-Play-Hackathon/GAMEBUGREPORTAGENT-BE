using FluentAssertions;
using GameBug.Application.BugReports.CreateReport;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class CreateReportValidatorTests
{
    private readonly CreateReportValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldRejectMismatchedMimeAndExtension()
    {
        var command = new CreateReportCommand(
            "A sufficiently long description", null, null, null, null, null,
            "1234567890123456",
            new[] { new FileAttachmentCommand("fake.png", "text/plain", new MemoryStream(new byte[] { 1 })) });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("do not match"));
    }

    [Fact]
    public async Task Validate_ShouldRejectMoreThanFiveFiles()
    {
        var files = Enumerable.Range(0, 6)
            .Select(index => new FileAttachmentCommand($"file{index}.txt", "text/plain", new MemoryStream(new byte[] { 1 })))
            .ToArray();
        var command = new CreateReportCommand(
            "A sufficiently long description", null, null, null, null, null,
            "1234567890123456", files);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }
}
