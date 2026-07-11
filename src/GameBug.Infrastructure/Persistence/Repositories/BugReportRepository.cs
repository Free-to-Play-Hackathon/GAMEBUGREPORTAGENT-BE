using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.BugReports;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public class BugReportRepository : IBugReportRepository
{
    private readonly GameBugDbContext _dbContext;

    public BugReportRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(BugReport report, CancellationToken cancellationToken)
    {
        await _dbContext.BugReports.AddAsync(report, cancellationToken);
    }

    public async Task<BugReport?> GetAsync(BugReportId id, CancellationToken cancellationToken)
    {
        return await _dbContext.BugReports
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
