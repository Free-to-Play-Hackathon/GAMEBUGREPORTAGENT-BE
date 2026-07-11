using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameBug.Application.Abstractions.AI;

namespace GameBug.Infrastructure.AI;

public class PromptLoader : IPromptLoader
{
    public async Task<PromptPackage> LoadAsync(string promptVersion, CancellationToken cancellationToken)
    {
        string versionDir = promptVersion.Trim().ToLowerInvariant();
        if (versionDir == "repro-v1")
        {
            versionDir = "v1";
        }

        string promptDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "GameBug.Infrastructure", "AI", "Prompts", "repro", versionDir);
        if (!Directory.Exists(promptDir))
        {
            promptDir = Path.Combine(AppContext.BaseDirectory, "AI", "Prompts", "repro", versionDir);
        }

        if (!Directory.Exists(promptDir))
        {
            throw new DirectoryNotFoundException($"Prompt directory not found: {promptDir}");
        }

        string systemInstruction = await File.ReadAllTextAsync(Path.Combine(promptDir, "system.txt"), cancellationToken);
        string schemaJson = await File.ReadAllTextAsync(Path.Combine(promptDir, "schema.json"), cancellationToken);
        string template = await File.ReadAllTextAsync(Path.Combine(promptDir, "template.txt"), cancellationToken);

        return new PromptPackage(systemInstruction, template, schemaJson);
    }
}
