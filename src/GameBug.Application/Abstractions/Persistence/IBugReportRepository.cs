using GameBug.Domain.BugReports;

namespace GameBug.Application.Abstractions.Persistence;

public interface IBugReportRepository
{
    Task AddAsync(BugReport report, CancellationToken cancellationToken);
    Task<BugReport?> GetAsync(BugReportId id, CancellationToken cancellationToken);
}
