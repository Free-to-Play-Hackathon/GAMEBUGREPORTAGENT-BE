namespace GameBug.Contracts.BugReports;

public record BugReportResponse(
    Guid ReportId,
    string Description,
    string? BuildVersion,
    string? Platform,
    string? Device,
    string? Locale,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AttachmentResponse> Attachments);
