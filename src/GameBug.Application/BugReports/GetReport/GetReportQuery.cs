using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.BugReports.GetReport;

public record AttachmentResult(
    Guid AttachmentId,
    string OriginalFileName,
    string AttachmentType,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    DateTimeOffset CreatedAt);

public record GetReportResult(
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
    IReadOnlyList<AttachmentResult> Attachments);

public record GetReportQuery(Guid ReportId) : IRequest<Result<GetReportResult>>;
