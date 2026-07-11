using System.Text.Json;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Evaluation;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Evaluation;

public sealed class FileEvaluationArtifactWriter : IEvaluationArtifactWriter
{
    private readonly EvaluationOptions _options;

    public FileEvaluationArtifactWriter(IOptions<EvaluationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> WriteAsync(EvaluationArtifact artifact, CancellationToken cancellationToken)
    {
        string root = Path.IsPathRooted(_options.ArtifactRoot)
            ? _options.ArtifactRoot
            : Path.Combine(Directory.GetCurrentDirectory(), _options.ArtifactRoot);
        Directory.CreateDirectory(root);

        string fileName = $"{artifact.ManifestId}-{artifact.RunId:N}.json";
        string finalPath = Path.GetFullPath(Path.Combine(root, fileName));
        string tempPath = $"{finalPath}.tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, artifact, FileEvaluationManifestLoader.JsonOptions, cancellationToken);
        }

        File.Move(tempPath, finalPath, overwrite: true);
        return finalPath;
    }
}
