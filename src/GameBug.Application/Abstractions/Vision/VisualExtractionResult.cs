using GameBug.Application.Vision;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;

namespace GameBug.Application.Abstractions.Vision;

public sealed record VisualExtractionResult(
    VisionStageOutcome Outcome,
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<AnalysisWarning> Warnings,
    string Provider,
    string StageVersion,
    int ProcessedAttachmentCount);
