using GameBug.Application.Evaluation;

namespace GameBug.Application.Abstractions.Evaluation;

public interface IEvaluationArtifactWriter
{
    Task<string> WriteAsync(EvaluationArtifact artifact, CancellationToken cancellationToken);
}
