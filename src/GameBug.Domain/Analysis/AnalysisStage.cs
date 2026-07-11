namespace GameBug.Domain.Analysis;

public enum AnalysisStage
{
    Sanitizing,
    ExtractingEvidence,
    ExtractingVisualEvidence,
    GroundingGameContext,
    GeneratingRepro,
    SearchingDuplicates,
    PersistingResult
}
