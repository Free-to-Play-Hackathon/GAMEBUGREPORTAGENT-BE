using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.BugReports;

public class BugReport
{
    private readonly List<Attachment> _attachments = new();

    // For EF Core
    private BugReport() { }

    private BugReport(
        BugReportId id,
        string description,
        string? buildVersion,
        string? platform,
        string? device,
        string? locale,
        string? sessionReference,
        string createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        Description = description.Trim();
        BuildVersion = buildVersion;
        Platform = platform;
        Device = device;
        Locale = locale;
        SessionReference = sessionReference;
        Status = ReportStatus.Submitted;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public BugReportId Id { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? BuildVersion { get; private set; }
    public string? Platform { get; private set; }
    public string? Device { get; private set; }
    public string? Locale { get; private set; }
    public string? SessionReference { get; private set; }
    public ReportStatus Status { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; } // Concurrency token

    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    public static Result<BugReport> Submit(
        BugReportId id,
        string description,
        string? buildVersion,
        string? platform,
        string? device,
        string? locale,
        string? sessionReference,
        string createdBy,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.DescriptionRequired", "Description cannot be empty."));
        }

        string trimmedDescription = description.Trim();
        if (trimmedDescription.Length < 10 || trimmedDescription.Length > 10000)
        {
            return Result.Failure<BugReport>(new DomainError(
                "BugReport.DescriptionInvalidLength",
                "Description must be between 10 and 10,000 characters."));
        }

        if (buildVersion is { Length: > 64 })
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.BuildVersionTooLong", "Build version cannot exceed 64 characters."));
        }

        if (platform is { Length: > 128 })
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.PlatformTooLong", "Platform cannot exceed 128 characters."));
        }

        if (device is { Length: > 128 })
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.DeviceTooLong", "Device cannot exceed 128 characters."));
        }

        if (locale is { Length: > 32 })
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.LocaleTooLong", "Locale cannot exceed 32 characters."));
        }

        if (sessionReference is { Length: > 256 })
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.SessionReferenceTooLong", "Session reference cannot exceed 256 characters."));
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            return Result.Failure<BugReport>(new DomainError("BugReport.CreatedByRequired", "Creator identifier is required."));
        }

        return new BugReport(id, trimmedDescription, buildVersion, platform, device, locale, sessionReference, createdBy, createdAt);
    }

    public Result AddAttachment(
        AttachmentId attachmentId,
        string storageKey,
        string originalFileName,
        AttachmentType attachmentType,
        string contentType,
        long sizeBytes,
        string checksum,
        DateTimeOffset createdAt)
    {
        string extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        bool metadataIsConsistent = attachmentType switch
        {
            AttachmentType.Screenshot =>
                extension == ".png" && contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                extension is ".jpg" or ".jpeg" && contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            AttachmentType.Log =>
                extension is ".log" or ".txt" &&
                (contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
                 contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
        if (!metadataIsConsistent)
        {
            return Result.Failure(new DomainError("BugReport.AttachmentMetadataMismatch", "Attachment type, extension and content type must match."));
        }

        if (storageKey.Contains(originalFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new DomainError("BugReport.StorageKeyContainsFileName", "Storage key must be opaque."));
        }

        if (_attachments.Count >= 5)
        {
            return Result.Failure(new DomainError("BugReport.MaxAttachmentsExceeded", "A bug report cannot have more than 5 attachments."));
        }

        if (sizeBytes <= 0)
        {
            return Result.Failure(new DomainError("BugReport.AttachmentEmpty", "Attachment size must be greater than zero."));
        }

        // Enforce maximum file sizes: Screenshots 8 MiB, Logs 10 MiB, Other 10 MiB
        long maxSizeBytes = attachmentType switch
        {
            AttachmentType.Screenshot => 8 * 1024 * 1024,
            AttachmentType.Log => 10 * 1024 * 1024,
            _ => 10 * 1024 * 1024
        };

        if (sizeBytes > maxSizeBytes)
        {
            return Result.Failure(new DomainError(
                "BugReport.AttachmentTooLarge",
                $"Attachment of type {attachmentType} exceeds maximum size of {maxSizeBytes / (1024 * 1024)} MiB."));
        }

        if (_attachments.Any(a => a.OriginalFileName.Equals(originalFileName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(new DomainError("BugReport.DuplicateAttachmentFileName", "An attachment with the same filename already exists."));
        }

        var attachment = new Attachment(
            attachmentId,
            Id,
            storageKey,
            originalFileName,
            attachmentType,
            contentType,
            sizeBytes,
            checksum,
            createdAt);

        _attachments.Add(attachment);
        UpdatedAt = createdAt;

        return Result.Success();
    }

    public Result UpdateStatus(ReportStatus newStatus, DateTimeOffset updatedAt)
    {
        bool isValid = (Status, newStatus) switch
        {
            (ReportStatus.Draft, ReportStatus.Submitted) => true,
            (ReportStatus.Submitted, ReportStatus.NeedsMoreInformation) => true,
            (ReportStatus.Submitted, ReportStatus.UnderReview) => true,
            (ReportStatus.Submitted, ReportStatus.Closed) => true,
            (ReportStatus.NeedsMoreInformation, ReportStatus.Submitted) => true,
            (ReportStatus.NeedsMoreInformation, ReportStatus.Closed) => true,
            (ReportStatus.UnderReview, ReportStatus.NeedsMoreInformation) => true,
            (ReportStatus.UnderReview, ReportStatus.Closed) => true,
            _ => false
        };

        if (!isValid)
        {
            return Result.Failure(new DomainError(
                "BugReport.InvalidStatusTransition",
                $"Cannot transition bug report from {Status} to {newStatus}."));
        }

        Status = newStatus;
        UpdatedAt = updatedAt;

        return Result.Success();
    }

    public Result ApplyClarifiedMetadata(string? buildVersion, string? platform, DateTimeOffset updatedAt)
    {
        buildVersion = string.IsNullOrWhiteSpace(buildVersion) ? null : buildVersion.Trim();
        platform = string.IsNullOrWhiteSpace(platform) ? null : platform.Trim();
        if (buildVersion is { Length: > 64 })
        {
            return Result.Failure(new DomainError("BugReport.BuildVersionTooLong", "Build version cannot exceed 64 characters."));
        }

        if (platform is { Length: > 128 })
        {
            return Result.Failure(new DomainError("BugReport.PlatformTooLong", "Platform cannot exceed 128 characters."));
        }

        BuildVersion = buildVersion ?? BuildVersion;
        Platform = platform ?? Platform;
        UpdatedAt = updatedAt;
        return Result.Success();
    }

    public Result BeginQaReview(DateTimeOffset startedAt)
    {
        return UpdateStatus(ReportStatus.UnderReview, startedAt);
    }

    public Result RequestMoreInformation(DateTimeOffset requestedAt)
    {
        return UpdateStatus(ReportStatus.NeedsMoreInformation, requestedAt);
    }

    public Result CloseAsDuplicate(DateTimeOffset closedAt)
    {
        return UpdateStatus(ReportStatus.Closed, closedAt);
    }

    public Result CloseWithNewTicket(DateTimeOffset closedAt)
    {
        return UpdateStatus(ReportStatus.Closed, closedAt);
    }
}
