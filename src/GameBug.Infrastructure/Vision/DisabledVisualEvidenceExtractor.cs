using GameBug.Application.Abstractions.Vision;
using GameBug.Application.Vision;
using GameBug.Domain.Analysis;

namespace GameBug.Infrastructure.Vision;

public sealed class DisabledVisualEvidenceExtractor : IVisualEvidenceExtractor
{
    public Task<VisualExtractionResult> ExtractAsync(VisualExtractionRequest request, CancellationToken cancellationToken)
    {
        var result = new VisualExtractionResult(
            VisionStageOutcome.Skipped,
            Array.Empty<GameBug.Domain.Evidence.EvidenceFact>(),
            new[] { new AnalysisWarning("VISION_DISABLED", "Visual evidence extraction is disabled.") },
            "Disabled",
            request.StageVersion,
            0);

        return Task.FromResult(result);
    }
}
