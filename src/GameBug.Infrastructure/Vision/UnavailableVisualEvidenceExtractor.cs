using GameBug.Application.Abstractions.Vision;
using GameBug.Application.Vision;
using GameBug.Domain.Analysis;

namespace GameBug.Infrastructure.Vision;

public sealed class UnavailableVisualEvidenceExtractor : IVisualEvidenceExtractor
{
    public Task<VisualExtractionResult> ExtractAsync(VisualExtractionRequest request, CancellationToken cancellationToken)
    {
        var result = new VisualExtractionResult(
            VisionStageOutcome.Degraded,
            Array.Empty<GameBug.Domain.Evidence.EvidenceFact>(),
            new[] { new AnalysisWarning("VISION_PROVIDER_UNAVAILABLE", "Visual evidence extraction is unavailable.") },
            request.Provider,
            request.StageVersion,
            0);

        return Task.FromResult(result);
    }
}
