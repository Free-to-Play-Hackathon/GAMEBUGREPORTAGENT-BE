namespace GameBug.Domain.Analysis;

public enum AnalysisStage
{
    Sanitizing,
    ExtractingEvidence,
    GroundingGameContext,
    GeneratingRepro,
    SearchingDuplicates,
    PersistingResult
}
