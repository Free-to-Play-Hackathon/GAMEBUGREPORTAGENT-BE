using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public sealed class EvaluationRunRepository : IEvaluationRunRepository
{
    private readonly GameBugDbContext _dbContext;

    public EvaluationRunRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EvaluationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.EvaluationRuns
            .Include(r => r.CaseResults)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddAsync(EvaluationRun run, CancellationToken cancellationToken)
    {
        await _dbContext.EvaluationRuns.AddAsync(run, cancellationToken);
    }

    public Task UpdateAsync(EvaluationRun run, CancellationToken cancellationToken)
    {
        _dbContext.EvaluationRuns.Update(run);
        return Task.CompletedTask;
    }
}
