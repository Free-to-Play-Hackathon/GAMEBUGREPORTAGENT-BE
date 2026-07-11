using GameBug.Domain.Evaluation;

namespace GameBug.Application.Abstractions.Persistence;

public interface IEvaluationRunRepository
{
    Task<EvaluationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(EvaluationRun run, CancellationToken cancellationToken);
    Task UpdateAsync(EvaluationRun run, CancellationToken cancellationToken);
}
