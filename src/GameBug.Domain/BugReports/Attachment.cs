using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.BugReports;

public class Attachment
{
    // For EF Core
    private Attachment() { }

    public Attachment(
        AttachmentId id,
        BugReportId bugReportId,
        string storageKey,
        string originalFileName,
        AttachmentType attachmentType,
        string contentType,
        long sizeBytes,
        string checksum,
        DateTimeOffset createdAt)
    {
        Id = id;
        BugReportId = bugReportId;
        StorageKey = storageKey;
        OriginalFileName = originalFileName;
        AttachmentType = attachmentType;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        ChecksumAlgorithm = "SHA256";
        Checksum = checksum;
        ScanStatus = ScanStatus.Pending;
        CreatedAt = createdAt;
    }

    public AttachmentId Id { get; private set; } = null!;
    public BugReportId BugReportId { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public AttachmentType AttachmentType { get; private set; }
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string ChecksumAlgorithm { get; private set; } = "SHA256";
    public string Checksum { get; private set; } = null!;
    public ScanStatus ScanStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkClean() => ScanStatus = ScanStatus.Clean;
    public void MarkRejected() => ScanStatus = ScanStatus.Rejected;
    public void MarkFailed() => ScanStatus = ScanStatus.Failed;
}
