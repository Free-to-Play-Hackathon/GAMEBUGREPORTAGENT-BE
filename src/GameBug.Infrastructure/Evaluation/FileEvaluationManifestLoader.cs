using System.Text.Json;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Evaluation;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Evaluation;

public sealed class FileEvaluationManifestLoader : IEvaluationManifestLoader
{
    private readonly EvaluationOptions _options;

    public FileEvaluationManifestLoader(IOptions<EvaluationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<EvaluationManifest?> LoadAsync(string manifestId, CancellationToken cancellationToken)
    {
        if (!IsAllowlisted(manifestId))
        {
            return null;
        }

        string path = ResolvePath(_options.ManifestRoot, $"{manifestId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<EvaluationManifest>(stream, JsonOptions, cancellationToken);
        return manifest is not null && manifest.ManifestId.Equals(manifestId, StringComparison.Ordinal)
            ? manifest
            : null;
    }

    private bool IsAllowlisted(string manifestId) =>
        _options.AllowlistedManifests.Any(allowed => allowed.Equals(manifestId, StringComparison.Ordinal)) &&
        manifestId.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    internal static string ResolvePath(string root, string fileName)
    {
        string basePath = Path.IsPathRooted(root)
            ? root
            : Path.Combine(Directory.GetCurrentDirectory(), root);
        return Path.GetFullPath(Path.Combine(basePath, fileName));
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
