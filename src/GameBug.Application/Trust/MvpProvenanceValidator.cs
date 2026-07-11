using System;
using System.Collections.Generic;
using System.Linq;
using GameBug.Application.Abstractions.Trust;
using GameBug.Application.ReproCases;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.Trust;

namespace GameBug.Application.Trust;

public class MvpProvenanceValidator : IProvenanceValidator
{
    public IReadOnlyList<TrustViolation> Validate(
        AnalysisRunId runId,
        EvidencePack evidencePack,
        ReproCase reproCase,
        IReadOnlyList<ReproValidatorWarning> warnings)
    {
        var violations = new List<TrustViolation>();

        if (evidencePack.AnalysisRunId != runId)
        {
            violations.Add(new TrustViolation(
                "CROSS_RUN_EVIDENCE_PACK",
                "evidencePack.analysisRunId",
                null,
                IsBlocking: true,
                "Evidence pack does not belong to the analysis run being validated."));
        }

        if (reproCase.AnalysisRunId != runId)
        {
            violations.Add(new TrustViolation(
                "CROSS_RUN_REPRO_CASE",
                "reproCase.analysisRunId",
                null,
                IsBlocking: true,
                "Repro case does not belong to the analysis run being validated."));
        }

        // 1. Map all sources in evidence pack
        var allSources = new Dictionary<Guid, EvidenceSource>();
        if (evidencePack?.Facts != null)
        {
            foreach (var fact in evidencePack.Facts)
            {
                if (fact.Sources != null)
                {
                    foreach (var source in fact.Sources)
                    {
                        allSources[source.Id] = source;
                    }
                }
            }
        }

        // 2. Propagate warnings from the deserializer/validator before any safe downgrade hides them.
        foreach (var warning in warnings.Where(w => w.Code is "FAKE_SOURCE" or "SUGGESTED_STEP_MISSING_REASON"))
        {
            Guid? attemptedGuid = null;
            if (Guid.TryParse(warning.AttemptedValue, out var parsedGuid))
            {
                attemptedGuid = parsedGuid;
            }

            violations.Add(new TrustViolation(
                warning.Code,
                warning.OutputPath ?? "reproCase.steps",
                attemptedGuid,
                IsBlocking: true,
                warning.Message));
        }

        // 3. Validate step references and type rules
        if (reproCase.Steps != null)
        {
            foreach (var step in reproCase.Steps)
            {
                if (step.StepType == StepType.Confirmed)
                {
                    if (step.SourceId == null)
                    {
                        violations.Add(new TrustViolation(
                            "CONFIRMED_STEP_MISSING_SOURCE",
                            $"reproCase.steps[{step.Order - 1}].sourceId",
                            null,
                            IsBlocking: true,
                            $"Step {step.Order} is Confirmed but is missing a SourceId."));
                    }
                    else
                    {
                        var sourceId = step.SourceId.Value;
                        if (!allSources.TryGetValue(sourceId, out var source))
                        {
                            violations.Add(new TrustViolation(
                                "FAKE_SOURCE",
                                $"reproCase.steps[{step.Order - 1}].sourceId",
                                sourceId,
                                IsBlocking: true,
                                $"Step {step.Order} references a source ID '{sourceId}' that does not exist in the evidence pack."));
                        }
                        else if (source.SourceType != EvidenceSourceType.Log && source.SourceType != EvidenceSourceType.Screenshot)
                        {
                            violations.Add(new TrustViolation(
                                "UNSUPPORTED_CONFIRMED_OUTPUT",
                                $"reproCase.steps[{step.Order - 1}].sourceId",
                                sourceId,
                                IsBlocking: true,
                                $"Step {step.Order} is Confirmed but references an unsupported source type '{source.SourceType}'. Confirmed steps must reference Log or Screenshot sources."));
                        }
                    }
                }
                else if (step.StepType == StepType.SuggestedToVerify)
                {
                    if (string.IsNullOrWhiteSpace(step.InferenceReason))
                    {
                        violations.Add(new TrustViolation(
                            "SUGGESTED_STEP_MISSING_REASON",
                            $"reproCase.steps[{step.Order - 1}].inferenceReason",
                            null,
                            IsBlocking: true,
                            $"Step {step.Order} is SuggestedToVerify but has no inference reason."));
                    }

                    if (step.SourceId != null)
                    {
                        violations.Add(new TrustViolation(
                            "SUGGESTED_STEP_HAS_SOURCE",
                            $"reproCase.steps[{step.Order - 1}].sourceId",
                            step.SourceId,
                            IsBlocking: true,
                            $"Step {step.Order} is SuggestedToVerify but has a source ID. Suggested steps must not have source IDs."));
                    }
                }
            }
        }

        // 4. Validate Unknown fields
        if (reproCase.BuildVersion == "Unknown")
        {
            violations.Add(new TrustViolation(
                "MISSING_BUILD_VERSION",
                "reproCase.buildVersion",
                null,
                IsBlocking: true,
                "Build version is Unknown. This is required for filing."));
        }

        if (reproCase.Platform == "Unknown")
        {
            violations.Add(new TrustViolation(
                "MISSING_PLATFORM",
                "reproCase.platform",
                null,
                IsBlocking: true,
                "Platform is Unknown. This is required for filing."));
        }

        // 5. Validate Conflicting fields
        if (evidencePack?.Facts != null)
        {
            var buildFact = evidencePack.Facts.FirstOrDefault(f => f.FactType == "buildVersion");
            if (buildFact != null && buildFact.Status == EvidenceStatus.Conflict)
            {
                violations.Add(new TrustViolation(
                    "BUILD_VERSION_CONFLICT",
                    "reproCase.buildVersion",
                    null,
                    IsBlocking: true,
                    $"Build version has conflicting evidence: {buildFact.NormalizedValue}"));
            }

            var platformFact = evidencePack.Facts.FirstOrDefault(f => f.FactType == "platform");
            if (platformFact != null && platformFact.Status == EvidenceStatus.Conflict)
            {
                violations.Add(new TrustViolation(
                    "PLATFORM_CONFLICT",
                    "reproCase.platform",
                    null,
                    IsBlocking: true,
                    $"Platform has conflicting evidence: {platformFact.NormalizedValue}"));
            }
        }

        return violations;
    }
}
