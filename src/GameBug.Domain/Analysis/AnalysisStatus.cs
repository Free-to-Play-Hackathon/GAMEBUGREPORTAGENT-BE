namespace GameBug.Domain.Analysis;

public enum AnalysisStatus
{
    Received,
    Queued,
    Processing,
    AwaitingQaReview,
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled
}
