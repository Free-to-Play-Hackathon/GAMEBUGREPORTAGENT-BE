using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.BugReports.CreateReport;

public record FileAttachmentCommand(
    string OriginalFileName,
    string ContentType,
    Stream ContentStream);

public record CreateReportCommand(
    string Description,
    string? BuildVersion,
    string? Platform,
    string? Device,
    string? Locale,
    string? SessionReference,
    string IdempotencyKey,
    IReadOnlyList<FileAttachmentCommand> Attachments) : IRequest<Result<CreateReportResult>>;

public record CreateReportResult(
    Guid ReportId,
    string Status,
    int AttachmentCount,
    DateTimeOffset CreatedAt);
