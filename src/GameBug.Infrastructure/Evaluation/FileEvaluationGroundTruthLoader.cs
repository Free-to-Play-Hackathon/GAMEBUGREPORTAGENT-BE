using System.Text.Json;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Evaluation;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Evaluation;

public sealed class FileEvaluationGroundTruthLoader : IEvaluationGroundTruthLoader
{
    private readonly EvaluationOptions _options;

    public FileEvaluationGroundTruthLoader(IOptions<EvaluationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<EvaluationGroundTruth?> LoadAsync(string manifestId, CancellationToken cancellationToken)
    {
        if (!_options.AllowlistedManifests.Any(allowed => allowed.Equals(manifestId, StringComparison.Ordinal)))
        {
            return null;
        }

        string path = FileEvaluationManifestLoader.ResolvePath(_options.GroundTruthRoot, $"{manifestId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<EvaluationGroundTruth>(
            stream,
            FileEvaluationManifestLoader.JsonOptions,
            cancellationToken);
    }
}
