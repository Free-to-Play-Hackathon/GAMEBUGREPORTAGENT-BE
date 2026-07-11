using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Analysis;
using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public class QaReviewRepository : IQaReviewRepository
{
    private readonly GameBugDbContext _dbContext;

    public QaReviewRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<QaReview?> GetByIdAsync(QaReviewId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QaReviews
            .Include(r => r.Revisions)
            .Include(r => r.Decision)
            .Include(r => r.InternalTicket)
            .Include(r => r.ClarificationRequests)
                .ThenInclude(c => c.Questions)
                    .ThenInclude(q => q.Answer)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<QaReview?> GetByAnalysisRunIdAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QaReviews
            .Include(r => r.Revisions)
            .Include(r => r.Decision)
            .Include(r => r.InternalTicket)
            .Include(r => r.ClarificationRequests)
                .ThenInclude(c => c.Questions)
                    .ThenInclude(q => q.Answer)
            .FirstOrDefaultAsync(r => r.AnalysisRunId == analysisRunId, cancellationToken);
    }

    public async Task AddAsync(QaReview review, CancellationToken cancellationToken = default)
    {
        await _dbContext.QaReviews.AddAsync(review, cancellationToken);
    }
}
