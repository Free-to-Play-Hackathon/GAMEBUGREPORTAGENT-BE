namespace GameBug.Contracts.BugReports;

public record AttachmentResponse(
    Guid AttachmentId,
    string OriginalFileName,
    string AttachmentType,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    DateTimeOffset CreatedAt);
