using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.BugReports;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.BugReports.GetReport;

public class GetReportHandler : IRequestHandler<GetReportQuery, Result<GetReportResult>>
{
    private readonly IBugReportRepository _bugReportRepository;
    private readonly ICurrentUser _currentUser;

    public GetReportHandler(
        IBugReportRepository bugReportRepository,
        ICurrentUser currentUser)
    {
        _bugReportRepository = bugReportRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<GetReportResult>> Handle(
        GetReportQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrEmpty(_currentUser.UserId))
        {
            return Result.Failure<GetReportResult>(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        var report = await _bugReportRepository.GetAsync(new BugReportId(query.ReportId), cancellationToken);

        if (report == null)
        {
            return Result.Failure<GetReportResult>(new DomainError("BugReport.NotFound", "The requested bug report was not found."));
        }

        // Access control: Only the creator of the report can retrieve it.
        // In later phases this can be expanded (e.g. QA role check), but for now we enforce user ownership.
        if (!report.CreatedBy.Equals(_currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<GetReportResult>(new DomainError("BugReport.Forbidden", "You do not have permission to view this report."));
        }

        var attachments = report.Attachments.Select(a => new AttachmentResult(
            a.Id.Value,
            a.OriginalFileName,
            a.AttachmentType.ToString(),
            a.ContentType,
            a.SizeBytes,
            a.ScanStatus.ToString(),
            a.CreatedAt)).ToList();

        var result = new GetReportResult(
            report.Id.Value,
            report.Description,
            report.BuildVersion,
            report.Platform,
            report.Device,
            report.Locale,
            report.Status.ToString(),
            report.CreatedBy,
            report.CreatedAt,
            report.UpdatedAt,
            attachments);

        return result;
    }
}
