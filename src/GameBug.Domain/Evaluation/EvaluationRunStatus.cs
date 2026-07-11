namespace GameBug.Domain.Evaluation;

public enum EvaluationRunStatus
{
    Queued,
    Running,
    Completed,
    CompletedWithErrors,
    Failed
}
