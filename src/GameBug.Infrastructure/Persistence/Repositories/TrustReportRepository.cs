using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Trust;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public class TrustReportRepository : ITrustReportRepository
{
    private readonly GameBugDbContext _dbContext;

    public TrustReportRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TrustReport report, CancellationToken cancellationToken)
    {
        await _dbContext.Set<TrustReport>().AddAsync(report, cancellationToken);
    }

    public async Task<TrustReport?> GetLatestForTargetAsync(Guid targetId, TrustTargetType targetType, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<TrustReport>()
            .Where(r => r.TargetId == targetId && r.TargetType == targetType)
            .OrderByDescending(r => r.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
