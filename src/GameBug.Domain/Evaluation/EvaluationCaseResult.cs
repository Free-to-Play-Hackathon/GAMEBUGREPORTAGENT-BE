using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Evaluation;

public class EvaluationCaseResult
{
    private EvaluationCaseResult() { }

    private EvaluationCaseResult(
        Guid id,
        Guid evaluationRunId,
        string caseId,
        AnalysisRunId? analysisRunId,
        EvaluationCaseOutcome outcome,
        string? expectedDuplicateKey,
        string? actualTopKey,
        int? actualRank,
        string? actualClassification,
        long? latencyMs,
        string? errorCode,
        DateTimeOffset createdAt)
    {
        Id = id;
        EvaluationRunId = evaluationRunId;
        CaseId = caseId.Trim();
        AnalysisRunId = analysisRunId;
        Outcome = outcome;
        ExpectedDuplicateKey = string.IsNullOrWhiteSpace(expectedDuplicateKey) ? null : expectedDuplicateKey.Trim();
        ActualTopKey = string.IsNullOrWhiteSpace(actualTopKey) ? null : actualTopKey.Trim();
        ActualRank = actualRank;
        ActualClassification = string.IsNullOrWhiteSpace(actualClassification) ? null : actualClassification.Trim();
        LatencyMs = latencyMs;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid EvaluationRunId { get; private set; }
    public string CaseId { get; private set; } = null!;
    public AnalysisRunId? AnalysisRunId { get; private set; }
    public EvaluationCaseOutcome Outcome { get; private set; }
    public string? ExpectedDuplicateKey { get; private set; }
    public string? ActualTopKey { get; private set; }
    public int? ActualRank { get; private set; }
    public string? ActualClassification { get; private set; }
    public long? LatencyMs { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<EvaluationCaseResult> Create(
        Guid evaluationRunId,
        string caseId,
        EvaluationCaseOutcome outcome,
        DateTimeOffset createdAt,
        AnalysisRunId? analysisRunId = null,
        string? expectedDuplicateKey = null,
        string? actualTopKey = null,
        int? actualRank = null,
        string? actualClassification = null,
        long? latencyMs = null,
        string? errorCode = null)
    {
        if (evaluationRunId == Guid.Empty)
        {
            return Result.Failure<EvaluationCaseResult>(new DomainError("EvaluationCase.RunRequired", "Evaluation run id is required."));
        }

        if (string.IsNullOrWhiteSpace(caseId))
        {
            return Result.Failure<EvaluationCaseResult>(new DomainError("EvaluationCase.CaseIdRequired", "Case id is required."));
        }

        if (actualRank is <= 0)
        {
            return Result.Failure<EvaluationCaseResult>(new DomainError("EvaluationCase.InvalidRank", "Actual rank must be positive."));
        }

        if (latencyMs is < 0)
        {
            return Result.Failure<EvaluationCaseResult>(new DomainError("EvaluationCase.InvalidLatency", "Latency cannot be negative."));
        }

        return new EvaluationCaseResult(
            Guid.NewGuid(),
            evaluationRunId,
            caseId,
            analysisRunId,
            outcome,
            expectedDuplicateKey,
            actualTopKey,
            actualRank,
            actualClassification,
            latencyMs,
            errorCode,
            createdAt);
    }
}

public enum EvaluationCaseOutcome
{
    Success,
    Failed,
    Skipped
}
