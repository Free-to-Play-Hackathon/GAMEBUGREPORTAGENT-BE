using GameBug.Application.Evaluation;

namespace GameBug.Application.Abstractions.Evaluation;

public interface IEvaluationManifestLoader
{
    Task<EvaluationManifest?> LoadAsync(string manifestId, CancellationToken cancellationToken);
}
