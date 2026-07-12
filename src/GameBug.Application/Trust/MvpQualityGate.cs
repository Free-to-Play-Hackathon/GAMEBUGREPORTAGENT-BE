using System;
using System.Collections.Generic;
using System.Linq;
using GameBug.Application.Abstractions.Trust;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.Trust;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.Trust;

public class MvpQualityGate : IQualityGate
{
    public Result<TrustReport> Evaluate(
        AnalysisRunId runId,
        Guid targetId,
        TrustTargetType targetType,
        IReadOnlyList<TrustViolation> provenanceViolations,
        EvidencePack evidencePack,
        ReproCase reproCase,
        bool duplicateSearchComplete,
        string inputHash)
    {
        var violations = new List<TrustViolation>(provenanceViolations);
        IReadOnlyCollection<EvidenceFact> facts = evidencePack.Facts;

        // 1. Map all sources
        var allSources = new Dictionary<Guid, EvidenceSource>();
        foreach (var fact in facts)
        {
            if (fact.Sources != null)
            {
                foreach (var source in fact.Sources)
                {
                    allSources[source.Id] = source;
                }
            }
        }

        // 2. Validate Duplicate Search completion
        if (!duplicateSearchComplete)
        {
            violations.Add(new TrustViolation(
                "DUPLICATE_SEARCH_INCOMPLETE",
                "analysisRun.duplicateStatus",
                null,
                IsBlocking: true,
                "Duplicate search has not completed."));
        }

        if (!HasSupportedFact(
                facts,
                "buildVersion",
                reproCase.BuildVersion,
                EvidenceSourceType.Metadata,
                EvidenceSourceType.Log))
        {
            violations.Add(new TrustViolation(
                "MISSING_BUILD_PROVENANCE",
                "reproCase.buildVersion",
                null,
                IsBlocking: true,
                "Build version must be supported by Metadata or Log evidence."));
        }

        if (!HasSupportedFact(
                facts,
                "platform",
                reproCase.Platform,
                EvidenceSourceType.Metadata,
                EvidenceSourceType.Log))
        {
            violations.Add(new TrustViolation(
                "MISSING_PLATFORM_PROVENANCE",
                "reproCase.platform",
                null,
                IsBlocking: true,
                "Platform must be supported by Metadata or Log evidence."));
        }

        // 3. Expected Result: Verify GameCatalog evidence exists for expected behavior.
        bool hasCatalogSource = HasAnySupportedFact(
            facts,
            new[] { "expectedResult", "expectedBehavior", "gameExpectedBehavior" },
            EvidenceSourceType.GameCatalog);
        if (!hasCatalogSource && !IsUnknown(reproCase.ExpectedResult))
        {
            violations.Add(new TrustViolation(
                "MISSING_CATALOG_CONTEXT",
                "reproCase.expectedResult",
                null,
                IsBlocking: false, // Non-blocking/warning
                "No GameCatalog source was found in the evidence pack to verify expected behavior."));
        }

        // 4. Actual Result: Verify concrete symptom/result evidence exists.
        bool hasActualEvidence = HasAnySupportedFact(
            facts,
            new[]
            {
                "actualResult",
                "crashException",
                "stackSignature",
                "errorCode",
                "serverResponse",
                "resourceDelta",
                "resourceAfter",
                "receivedRewardCount",
                "action",
                "screen",
                "visualScreen",
                "visualErrorMessage",
                "visualUiState"
            },
            EvidenceSourceType.Log,
            EvidenceSourceType.PlayerReport,
            EvidenceSourceType.Screenshot);
        if (!hasActualEvidence)
        {
            violations.Add(new TrustViolation(
                "MISSING_ACTUAL_CONTEXT",
                "reproCase.actualResult",
                null,
                IsBlocking: true, // Blocking
                "No Log, PlayerReport, or Screenshot source was found in the evidence pack to verify actual behavior."));
        }

        // 5. Create immutable trust report using the static factory
        var reportId = TrustReportId.CreateUnique();
        var evaluatedAt = DateTimeOffset.UtcNow;
        var trustReportResult = TrustReport.Create(
            reportId,
            runId,
            targetId,
            targetType,
            TrustPolicyVersion.MvpV1,
            violations,
            inputHash,
            evaluatedAt);

        return trustReportResult;
    }

    private static bool HasSupportedFact(
        IEnumerable<EvidenceFact> facts,
        string factType,
        string value,
        params EvidenceSourceType[] eligibleSourceTypes)
    {
        if (IsUnknown(value))
        {
            return false;
        }

        return facts.Any(fact =>
            fact.FactType == factType &&
            (fact.Status == EvidenceStatus.Supported || fact.Status == EvidenceStatus.Corroborated) &&
            string.Equals(fact.NormalizedValue, value, StringComparison.OrdinalIgnoreCase) &&
            fact.Sources.Any(source => eligibleSourceTypes.Contains(source.SourceType)));
    }

    private static bool HasAnySupportedFact(
        IEnumerable<EvidenceFact> facts,
        IReadOnlyCollection<string> factTypes,
        params EvidenceSourceType[] eligibleSourceTypes)
    {
        return facts.Any(fact =>
            factTypes.Contains(fact.FactType, StringComparer.OrdinalIgnoreCase) &&
            (fact.Status == EvidenceStatus.Supported || fact.Status == EvidenceStatus.Corroborated) &&
            !string.IsNullOrWhiteSpace(fact.NormalizedValue) &&
            fact.Sources.Any(source => eligibleSourceTypes.Contains(source.SourceType)));
    }

    private static bool IsUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
}
