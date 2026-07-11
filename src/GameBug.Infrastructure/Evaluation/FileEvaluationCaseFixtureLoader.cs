using System.Text.Json;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Evaluation;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Evaluation;

public sealed class FileEvaluationCaseFixtureLoader : IEvaluationCaseFixtureLoader
{
    private readonly EvaluationOptions _options;

    public FileEvaluationCaseFixtureLoader(IOptions<EvaluationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<EvaluationCaseFixture?> LoadAsync(string caseId, CancellationToken cancellationToken)
    {
        string path = FileEvaluationManifestLoader.ResolvePath(
            _options.CaseRoot,
            Path.Combine(caseId, "report.json"));
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var fixture = await JsonSerializer.DeserializeAsync<EvaluationCaseFixture>(
            stream,
            FileEvaluationManifestLoader.JsonOptions,
            cancellationToken);
        if (fixture is null)
        {
            return null;
        }

        string crashLogPath = FileEvaluationManifestLoader.ResolvePath(
            _options.CaseRoot,
            Path.Combine(caseId, "crash.log"));
        if (!File.Exists(crashLogPath))
        {
            return fixture;
        }

        string crashLogText = await File.ReadAllTextAsync(crashLogPath, cancellationToken);
        return fixture with { CrashLogText = crashLogText };
    }
}
