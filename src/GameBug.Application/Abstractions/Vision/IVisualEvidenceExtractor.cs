using GameBug.Application.Vision;

namespace GameBug.Application.Abstractions.Vision;

public interface IVisualEvidenceExtractor
{
    Task<VisualExtractionResult> ExtractAsync(VisualExtractionRequest request, CancellationToken cancellationToken);
}
