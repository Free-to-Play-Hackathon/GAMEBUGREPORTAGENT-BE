using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.ReproCases;

public class ReproCase
{
    private readonly List<ReproStep> _steps = new();

    // For EF Core
    private ReproCase() { }

    private ReproCase(
        Guid id,
        AnalysisRunId analysisRunId,
        string title,
        string buildVersion,
        string platform,
        string preconditions,
        IEnumerable<ReproStep> steps,
        string expectedResult,
        string actualResult,
        Severity severityEstimate,
        string severityReason,
        string? missingInformation,
        ConfidenceScore confidence)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        Title = title.Trim();
        BuildVersion = string.IsNullOrWhiteSpace(buildVersion) ? "Unknown" : buildVersion.Trim();
        Platform = string.IsNullOrWhiteSpace(platform) ? "Unknown" : platform.Trim();
        Preconditions = preconditions.Trim();
        _steps.AddRange(steps.OrderBy(s => s.Order));
        ExpectedResult = expectedResult.Trim();
        ActualResult = actualResult.Trim();
        SeverityEstimate = severityEstimate;
        SeverityReason = severityReason.Trim();
        MissingInformation = missingInformation?.Trim();
        Confidence = confidence;
    }

    public Guid Id { get; private set; }
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string BuildVersion { get; private set; } = null!;
    public string Platform { get; private set; } = null!;
    public string Preconditions { get; private set; } = null!;
    public string ExpectedResult { get; private set; } = null!;
    public string ActualResult { get; private set; } = null!;
    public Severity SeverityEstimate { get; private set; }
    public string SeverityReason { get; private set; } = null!;
    public string? MissingInformation { get; private set; }
    public ConfidenceScore Confidence { get; private set; } = null!;

    public IReadOnlyCollection<ReproStep> Steps => _steps.AsReadOnly();

    public static Result<ReproCase> Create(
        Guid id,
        AnalysisRunId analysisRunId,
        string title,
        string buildVersion,
        string platform,
        string preconditions,
        IEnumerable<ReproStep> steps,
        string expectedResult,
        string actualResult,
        Severity severityEstimate,
        string severityReason,
        string? missingInformation,
        ConfidenceScore confidence)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<ReproCase>(new DomainError("ReproCase.TitleRequired", "Title is required."));
        }

        if (string.IsNullOrWhiteSpace(actualResult))
        {
            return Result.Failure<ReproCase>(new DomainError("ReproCase.ActualResultRequired", "Actual result is required."));
        }

        if (string.IsNullOrWhiteSpace(severityReason))
        {
            return Result.Failure<ReproCase>(new DomainError("ReproCase.SeverityReasonRequired", "Severity estimation reason is required."));
        }

        var stepList = steps.ToList();
        if (stepList.Count == 0 || stepList.Any(step => step.Order <= 0 || string.IsNullOrWhiteSpace(step.Description)) ||
            stepList.Select(step => step.Order).Distinct().Count() != stepList.Count)
        {
            return Result.Failure<ReproCase>(new DomainError(
                "ReproCase.InvalidSteps",
                "Repro steps must be non-empty and have unique positive order values."));
        }
        foreach (var step in stepList)
        {
            if (step.StepType == StepType.Confirmed && step.SourceId == null)
            {
                return Result.Failure<ReproCase>(new DomainError(
                    "ReproCase.ConfirmedStepMissingSource",
                    $"Confirmed step order {step.Order} must have a direct source ID."));
            }

            if (step.StepType == StepType.SuggestedToVerify && string.IsNullOrWhiteSpace(step.InferenceReason))
            {
                return Result.Failure<ReproCase>(new DomainError(
                    "ReproCase.SuggestedStepMissingReason",
                    $"Suggested step order {step.Order} must have an inference reason."));
            }
        }

        return new ReproCase(
            id,
            analysisRunId,
            title,
            buildVersion,
            platform,
            preconditions,
            stepList,
            expectedResult,
            actualResult,
            severityEstimate,
            severityReason,
            missingInformation,
            confidence);
    }
}
