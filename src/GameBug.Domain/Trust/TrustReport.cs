using System;
using System.Collections.Generic;
using System.Linq;
using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Trust;

public record TrustReportId(Guid Value)
{
    public static TrustReportId CreateUnique() => new(Guid.NewGuid());
}

public class TrustReport
{
    private readonly List<TrustViolation> _violations = new();
    private readonly List<AllowedQaAction> _allowedActions = new();

    // For EF Core
    private TrustReport() { }

    private TrustReport(
        TrustReportId id,
        AnalysisRunId analysisRunId,
        Guid targetId,
        TrustTargetType targetType,
        string policyVersion,
        QualityOutcome outcome,
        IEnumerable<AllowedQaAction> allowedActions,
        IEnumerable<TrustViolation> violations,
        string inputHash,
        DateTimeOffset evaluatedAt)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        TargetId = targetId;
        TargetType = targetType;
        PolicyVersion = policyVersion;
        Outcome = outcome;
        _allowedActions.AddRange(allowedActions);
        _violations.AddRange(violations);
        InputHash = inputHash;
        EvaluatedAt = evaluatedAt;
    }

    public TrustReportId Id { get; private set; } = null!;
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public Guid TargetId { get; private set; }
    public TrustTargetType TargetType { get; private set; }
    public string PolicyVersion { get; private set; } = null!;
    public QualityOutcome Outcome { get; private set; }
    public string InputHash { get; private set; } = null!;
    public DateTimeOffset EvaluatedAt { get; private set; }

    public IReadOnlyCollection<AllowedQaAction> AllowedActions => _allowedActions.AsReadOnly();
    public IReadOnlyCollection<TrustViolation> Violations => _violations.AsReadOnly();

    public static Result<TrustReport> Create(
        TrustReportId id,
        AnalysisRunId analysisRunId,
        Guid targetId,
        TrustTargetType targetType,
        string policyVersion,
        IEnumerable<TrustViolation> violations,
        string inputHash,
        DateTimeOffset evaluatedAt)
    {
        if (string.IsNullOrWhiteSpace(policyVersion))
        {
            return Result.Failure<TrustReport>(new DomainError("TrustReport.PolicyVersionRequired", "Policy version is required."));
        }

        var violationList = violations.ToList();

        // 1. Calculate outcome based on violations
        bool hasCritical = violationList.Any(v => 
            v.Code is "SCHEMA_INVALID" or "FAKE_SOURCE" or "UNSUPPORTED_CONFIRMED_OUTPUT" or "SUGGESTED_STEP_MISSING_REASON" or "UNKNOWN_HAS_VALUE" or
                "CROSS_RUN_EVIDENCE_PACK" or "CROSS_RUN_REPRO_CASE");

        QualityOutcome outcome;
        if (hasCritical)
        {
            outcome = QualityOutcome.Rejected;
        }
        else if (violationList.Any(v => v.IsBlocking))
        {
            outcome = QualityOutcome.NeedsMoreInformation;
        }
        else if (violationList.Any())
        {
            outcome = QualityOutcome.PassedWithWarnings;
        }
        else
        {
            outcome = QualityOutcome.Passed;
        }

        // 2. Calculate allowed actions based on outcome
        var allowedActions = new List<AllowedQaAction>();

        if (outcome == QualityOutcome.Rejected)
        {
            allowedActions.Add(AllowedQaAction.RejectAnalysis);
        }

        if (outcome != QualityOutcome.Rejected)
        {
            allowedActions.Add(AllowedQaAction.RequestMoreInformation);
            allowedActions.Add(AllowedQaAction.MarkDuplicate);

            if (outcome is QualityOutcome.Passed or QualityOutcome.PassedWithWarnings)
            {
                allowedActions.Add(AllowedQaAction.EditAndCreateNew);
            }
        }

        return new TrustReport(
            id,
            analysisRunId,
            targetId,
            targetType,
            policyVersion,
            outcome,
            allowedActions,
            violationList,
            inputHash,
            evaluatedAt);
    }
}
