using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.ReproCases;

public class ReproValidator : IReproValidator
{
    private readonly SeverityPolicy _severityPolicy;

    public ReproValidator(SeverityPolicy severityPolicy)
    {
        _severityPolicy = severityPolicy;
    }

    public ReproValidationResult ValidateAndConstruct(
        AnalysisRunId runId,
        string rawLlmResponseJson,
        IReadOnlyList<EvidenceFact> facts,
        string reportTitle)
    {
        var warnings = new List<ReproValidatorWarning>();

        LlmReproResponse? ltr;
        try
        {
            ltr = JsonSerializer.Deserialize<LlmReproResponse>(rawLlmResponseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new ReproValidationResult(
                Result.Failure<ReproCase>(new DomainError("INVALID_AI_SCHEMA", "Output JSON is invalid.")),
                warnings);
        }

        if (ltr == null)
        {
            return new ReproValidationResult(
                Result.Failure<ReproCase>(new DomainError("INVALID_AI_SCHEMA", "Output is empty.")),
                warnings);
        }

        var validationResult = ValidateAiResponse(ltr);
        if (validationResult.IsFailure)
        {
            return new ReproValidationResult(
                Result.Failure<ReproCase>(validationResult.Error),
                warnings);
        }

        var severityEnum = Enum.TryParse<Severity>(ltr.SeverityEstimate, true, out var parsedSev) ? parsedSev : Severity.Medium;
        var (finalSeverity, finalSeverityReason) = _severityPolicy.EstimateSeverity(facts, severityEnum, ltr.SeverityReason ?? "");

        var steps = new List<ReproStep>();
        foreach (var step in ltr.Steps ?? Array.Empty<LlmReproStep>())
        {
            var stepType = Enum.TryParse<StepType>(step.StepType, true, out var parsedType) ? parsedType : StepType.SuggestedToVerify;
            string? inferenceReason = step.InferenceReason;

            Guid? sourceId = null;
            if (stepType == StepType.Confirmed)
            {
                var allSources = facts.SelectMany(f => f.Sources).ToList();
                bool hasValidSource = false;
                if (!string.IsNullOrEmpty(step.SourceId) && Guid.TryParse(step.SourceId, out var parsedGuid))
                {
                    var matchingSource = allSources.FirstOrDefault(s => s.Id == parsedGuid);
                    if (matchingSource != null)
                    {
                        sourceId = parsedGuid;
                        hasValidSource = true;
                    }
                }

                if (!hasValidSource)
                {
                    warnings.Add(new ReproValidatorWarning(
                        "FAKE_SOURCE",
                        $"Step {step.Order} of type Confirmed has a fake or missing source ID: '{step.SourceId}'.",
                        $"steps[{step.Order - 1}].sourceId",
                        step.SourceId));

                    // Fallback suggested to verify if missing direct source
                    stepType = StepType.SuggestedToVerify;
                    inferenceReason ??= "The model supplied no resolvable direct source.";
                }
            }

            if (stepType == StepType.SuggestedToVerify && string.IsNullOrWhiteSpace(inferenceReason))
            {
                warnings.Add(new ReproValidatorWarning(
                    "SUGGESTED_STEP_MISSING_REASON",
                    $"Step {step.Order} of type SuggestedToVerify has no inference reason.",
                    $"steps[{step.Order - 1}].inferenceReason"));
                inferenceReason = "The model supplied no inference reason.";
            }

            steps.Add(new ReproStep(
                Guid.NewGuid(),
                step.Order,
                step.Description ?? "",
                stepType,
                sourceId,
                stepType == StepType.SuggestedToVerify ? inferenceReason : null
            ));
        }

        // Calculate confidence based on evidence coverage
        double evidenceConfidence = facts.Count == 0 ? 0 : facts.Average(fact => fact.Confidence);
        var confidenceScore = ConfidenceScore.Create(Math.Clamp(evidenceConfidence, 0, 1)).Value;

        string validatedBuild = IsSupportedValue(ltr.BuildVersion, facts, "buildVersion") ? ltr.BuildVersion! : "Unknown";
        string validatedPlatform = IsSupportedValue(ltr.Platform, facts, "platform") ? ltr.Platform! : "Unknown";

        // Normalise steps order (sorted by Order)
        var orderedSteps = steps.OrderBy(s => s.Order).ToList();

        var reproCaseResult = ReproCase.Create(
            Guid.NewGuid(),
            runId,
            ltr.Title ?? reportTitle,
            validatedBuild,
            validatedPlatform,
            ltr.Preconditions ?? "",
            orderedSteps,
            ltr.ExpectedResult!,
            ltr.ActualResult!,
            finalSeverity,
            finalSeverityReason,
            ltr.MissingInformation,
            confidenceScore);

        return new ReproValidationResult(reproCaseResult, warnings);
    }

    private static Result ValidateAiResponse(LlmReproResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Title) ||
            string.IsNullOrWhiteSpace(response.ExpectedResult) ||
            string.IsNullOrWhiteSpace(response.ActualResult) ||
            string.IsNullOrWhiteSpace(response.SeverityReason) ||
            response.Steps is null || response.Steps.Length == 0 ||
            response.Confidence is < 0 or > 1 ||
            !Enum.TryParse<Severity>(response.SeverityEstimate, true, out _))
        {
            return Result.Failure(new DomainError("INVALID_AI_SCHEMA", "JSON schema validation failed."));
        }

        if (response.Steps.Any(step => step.Order <= 0 || string.IsNullOrWhiteSpace(step.Description) ||
            !Enum.TryParse<StepType>(step.StepType, true, out _)))
        {
            return Result.Failure(new DomainError("INVALID_AI_SCHEMA", "Steps validation failed."));
        }

        return Result.Success();
    }

    private static bool IsSupportedValue(string? value, IEnumerable<EvidenceFact> facts, string factType)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return facts.Any(fact => fact.FactType == factType &&
            (fact.Status == EvidenceStatus.Supported || fact.Status == EvidenceStatus.Corroborated) &&
            string.Equals(fact.NormalizedValue, value, StringComparison.OrdinalIgnoreCase));
    }

    private class LlmReproResponse
    {
        public string? Title { get; set; }
        public string? BuildVersion { get; set; }
        public string? Platform { get; set; }
        public string? Preconditions { get; set; }
        public LlmReproStep[]? Steps { get; set; }
        public string? ExpectedResult { get; set; }
        public string? ActualResult { get; set; }
        public string? SeverityEstimate { get; set; }
        public string? SeverityReason { get; set; }
        public string? MissingInformation { get; set; }
        public double Confidence { get; set; }
    }

    private class LlmReproStep
    {
        public int Order { get; set; }
        public string? Description { get; set; }
        public string? StepType { get; set; }
        public string? SourceId { get; set; }
        public string? InferenceReason { get; set; }
    }
}
