using GameBug.Domain.Analysis;
using GameBug.Domain.QaWorkflow;

namespace GameBug.Application.Abstractions.Persistence;

public interface IQaReviewRepository
{
    Task<QaReview?> GetByIdAsync(QaReviewId id, CancellationToken cancellationToken = default);
    Task<QaReview?> GetByAnalysisRunIdAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default);
    Task AddAsync(QaReview review, CancellationToken cancellationToken = default);
}
