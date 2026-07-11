using System.Threading;
using System.Threading.Tasks;

namespace GameBug.Application.Abstractions.AI;

public sealed record PromptPackage(string SystemInstruction, string Template, string SchemaJson);

public interface IPromptLoader
{
    Task<PromptPackage> LoadAsync(string promptVersion, CancellationToken cancellationToken);
}
