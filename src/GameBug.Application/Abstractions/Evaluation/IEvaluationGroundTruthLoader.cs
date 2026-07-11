using GameBug.Application.Evaluation;

namespace GameBug.Application.Abstractions.Evaluation;

public interface IEvaluationGroundTruthLoader
{
    Task<EvaluationGroundTruth?> LoadAsync(string manifestId, CancellationToken cancellationToken);
}
