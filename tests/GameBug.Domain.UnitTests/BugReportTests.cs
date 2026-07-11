using FluentAssertions;
using GameBug.Domain.BugReports;
using GameBug.Domain.SharedKernel;
using Xunit;

namespace GameBug.Domain.UnitTests;

public class BugReportTests
{
    [Fact]
    public void Submit_ShouldReturnSuccess_WhenParametersAreValid()
    {
        // Arrange
        var id = BugReportId.CreateUnique();
        string description = "This is a valid bug report description with more than 10 characters.";
        string build = "1.0.0";
        string platform = "PC";
        string device = "Desktop";
        string locale = "en-US";
        string session = "session-123";
        string user = "User1";
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = BugReport.Submit(id, description, build, platform, device, locale, session, user, now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(id);
        result.Value.Description.Should().Be(description);
        result.Value.Status.Should().Be(ReportStatus.Submitted);
    }

    [Fact]
    public void Submit_ShouldReturnFailure_WhenDescriptionIsTooShort()
    {
        // Arrange
        var id = BugReportId.CreateUnique();
        string description = "Short"; // < 10 chars
        string user = "User1";

        // Act
        var result = BugReport.Submit(id, description, null, null, null, null, null, user, DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BugReport.DescriptionInvalidLength");
    }

    [Fact]
    public void AddAttachment_ShouldReturnSuccess_WhenAttachmentIsValid()
    {
        // Arrange
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Valid description for bug report testing.",
            null, null, null, null, null,
            "User1",
            DateTimeOffset.UtcNow).Value;

        // Act
        var result = report.AddAttachment(
            AttachmentId.CreateUnique(),
            "key1",
            "file.png",
            AttachmentType.Screenshot,
            "image/png",
            1024 * 1024, // 1MB
            "checksum123",
            DateTimeOffset.UtcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        report.Attachments.Should().HaveCount(1);
    }

    [Fact]
    public void AddAttachment_ShouldAcceptBrowserOctetStreamForLogFiles()
    {
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Valid description for browser log upload.",
            null, null, null, null, null,
            "User1",
            DateTimeOffset.UtcNow).Value;

        var result = report.AddAttachment(
            AttachmentId.CreateUnique(),
            "opaque-key",
            "inventory-crash.log",
            AttachmentType.Log,
            "application/octet-stream",
            1024,
            "checksum",
            DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        report.Attachments.Should().ContainSingle();
    }

    [Fact]
    public void AddAttachment_ShouldFail_WhenLimitExceeded()
    {
        // Arrange
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Valid description for bug report testing.",
            null, null, null, null, null,
            "User1",
            DateTimeOffset.UtcNow).Value;

        for (int i = 0; i < 5; i++)
        {
            report.AddAttachment(
                AttachmentId.CreateUnique(),
                $"key{i}",
                $"file{i}.png",
                AttachmentType.Screenshot,
                "image/png",
                1024,
                "checksum",
                DateTimeOffset.UtcNow);
        }

        // Act
        var result = report.AddAttachment(
            AttachmentId.CreateUnique(),
            "key6",
            "file6.png",
            AttachmentType.Screenshot,
            "image/png",
            1024,
            "checksum",
            DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BugReport.MaxAttachmentsExceeded");
    }

    [Fact]
    public void AddAttachment_ShouldFail_WhenDuplicateFileNameAdded()
    {
        // Arrange
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Valid description for bug report testing.",
            null, null, null, null, null,
            "User1",
            DateTimeOffset.UtcNow).Value;

        report.AddAttachment(
            AttachmentId.CreateUnique(),
            "key1",
            "duplicate.png",
            AttachmentType.Screenshot,
            "image/png",
            1024,
            "checksum",
            DateTimeOffset.UtcNow);

        // Act
        var result = report.AddAttachment(
            AttachmentId.CreateUnique(),
            "key2",
            "duplicate.png",
            AttachmentType.Screenshot,
            "image/png",
            1024,
            "checksum",
            DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BugReport.DuplicateAttachmentFileName");
    }

    [Fact]
    public void UpdateStatus_ShouldReturnFailure_WhenInvalidTransitionAttempted()
    {
        // Arrange
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Valid description for bug report testing.",
            null, null, null, null, null,
            "User1",
            DateTimeOffset.UtcNow).Value;

        // Status starts as Submitted. Transitioning to Draft should fail.
        // Act
        var result = report.UpdateStatus(ReportStatus.Draft, DateTimeOffset.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BugReport.InvalidStatusTransition");
    }

    [Fact]
    public void ApplyClarifiedMetadata_ShouldReplacePlaceholderMetadata()
    {
        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Valid description for bug report testing.",
            "string", "string", null, null, null,
            "User1",
            DateTimeOffset.UtcNow).Value;

        var result = report.ApplyClarifiedMetadata("1.2.3", "Windows", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        report.BuildVersion.Should().Be("1.2.3");
        report.Platform.Should().Be("Windows");
    }
}
