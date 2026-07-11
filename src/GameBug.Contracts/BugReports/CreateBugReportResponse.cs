namespace GameBug.Contracts.BugReports;

public record CreateBugReportResponse(
    Guid ReportId,
    string Status,
    int AttachmentCount,
    DateTimeOffset CreatedAt,
    string ResourceUrl);
