using GameBug.Domain.BugReports;
using GameBug.Domain.Evidence;
using GameBug.Application.Abstractions.Parsing;

namespace GameBug.Application.Evidence;

public class EvidenceResolver
{
    public void AppendGameplayFacts(List<EvidenceFact> facts, GameplayLogFacts? gameplay, string sourceRef)
    {
        if (gameplay is null)
        {
            return;
        }

        AddObservedFact(facts, "screen", gameplay.Screen, sourceRef);
        AddObservedFact(facts, "action", gameplay.Action, sourceRef);
        AddObservedFact(facts, "resourceType", gameplay.ResourceType, sourceRef);
        AddObservedFact(facts, "serverResponse", gameplay.ServerResponse, sourceRef);
        AddObservedFact(facts, "errorCode", gameplay.ErrorCode, sourceRef);
        AddObservedFact(facts, "resourceBefore", gameplay.ResourceBefore?.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceRef);
        AddObservedFact(facts, "resourceAfter", gameplay.ResourceAfter?.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceRef);
        AddObservedFact(facts, "expectedRewardCount", gameplay.ExpectedRewardCount?.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceRef);
        AddObservedFact(facts, "receivedRewardCount", gameplay.ReceivedRewardCount?.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceRef);

        if (gameplay.ResourceBefore.HasValue && gameplay.ResourceAfter.HasValue)
        {
            AddObservedFact(
                facts,
                "resourceDelta",
                (gameplay.ResourceAfter.Value - gameplay.ResourceBefore.Value).ToString(System.Globalization.CultureInfo.InvariantCulture),
                sourceRef);
        }
    }

    private static void AddObservedFact(List<EvidenceFact> facts, string factType, string? value, string sourceRef)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string excerpt = $"{factType}={value}";
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(excerpt))).ToLowerInvariant();
        var source = new EvidenceSource(
            EvidenceSourceType.Log,
            sourceRef,
            null,
            null,
            excerpt,
            hash,
            TrustLevel.Observed);
        var created = EvidenceFact.Create(
            Guid.NewGuid(), factType, value, EvidenceStatus.Supported, 0.95, new[] { source });
        if (created.IsSuccess)
        {
            facts.Add(created.Value);
        }
    }

    public List<EvidenceFact> ResolveFacts(
        BugReport report,
        string? logBuildVersion,
        string? logPlatform,
        string? exceptionType,
        string? exceptionMessage,
        string? stackSignatureHash,
        string reportSourceRef,
        string logSourceRef,
        string? sanitizedReportBuildVersion = null,
        string? sanitizedReportPlatform = null)
    {
        var facts = new List<EvidenceFact>();

        // 1. Resolve Build Version
        var buildFact = ResolveBuildVersion(
            sanitizedReportBuildVersion ?? report.BuildVersion, logBuildVersion, reportSourceRef, logSourceRef);
        facts.Add(buildFact);

        // 2. Resolve Platform
        var platformFact = ResolvePlatform(
            sanitizedReportPlatform ?? report.Platform, logPlatform, reportSourceRef, logSourceRef);
        facts.Add(platformFact);

        // 3. Resolve Exception Fact
        if (!string.IsNullOrEmpty(exceptionType))
        {
            var sources = new List<EvidenceSource>
            {
                new(
                    EvidenceSourceType.Log,
                    logSourceRef,
                    lineStart: null,
                    lineEnd: null,
                    sanitizedExcerpt: $"{exceptionType}: {exceptionMessage}",
                    excerptHash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(exceptionType))).ToLowerInvariant(),
                    TrustLevel.Observed)
            };

            var exceptionFact = EvidenceFact.Create(
                Guid.NewGuid(),
                "crashException",
                exceptionType,
                EvidenceStatus.Supported,
                confidence: 0.95,
                sources).Value;

            facts.Add(exceptionFact);
        }

        // 4. Resolve Stack Signature Fact
        if (!string.IsNullOrEmpty(stackSignatureHash))
        {
            var sources = new List<EvidenceSource>
            {
                new(
                    EvidenceSourceType.Log,
                    logSourceRef,
                    lineStart: null,
                    lineEnd: null,
                    sanitizedExcerpt: $"Stack trace signature: {stackSignatureHash}",
                    excerptHash: stackSignatureHash,
                    TrustLevel.Observed)
            };

            var signatureFact = EvidenceFact.Create(
                Guid.NewGuid(),
                "stackSignature",
                stackSignatureHash,
                EvidenceStatus.Supported,
                confidence: 0.95,
                sources).Value;

            facts.Add(signatureFact);
        }

        return facts;
    }

    private EvidenceFact ResolveBuildVersion(string? reportBuildVersion, string? logBuildVersion, string reportSourceRef, string logSourceRef)
    {
        var sources = new List<EvidenceSource>();

        if (!string.IsNullOrWhiteSpace(reportBuildVersion))
        {
            sources.Add(new(
                EvidenceSourceType.PlayerReport,
                reportSourceRef,
                lineStart: null,
                lineEnd: null,
                sanitizedExcerpt: $"Player reported build: {reportBuildVersion}",
                excerptHash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reportBuildVersion))).ToLowerInvariant(),
                TrustLevel.UserStructured));
        }

        if (!string.IsNullOrWhiteSpace(logBuildVersion))
        {
            sources.Add(new(
                EvidenceSourceType.Log,
                logSourceRef,
                lineStart: null,
                lineEnd: null,
                sanitizedExcerpt: $"Log detected build: {logBuildVersion}",
                excerptHash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(logBuildVersion))).ToLowerInvariant(),
                TrustLevel.Observed));
        }

        if (sources.Count == 2)
        {
            if (reportBuildVersion == logBuildVersion)
            {
                return EvidenceFact.Create(
                    Guid.NewGuid(),
                    "buildVersion",
                    logBuildVersion,
                    EvidenceStatus.Corroborated,
                    confidence: 1.0,
                    sources).Value;
            }
            else
            {
                // Conflict
                return EvidenceFact.Create(
                    Guid.NewGuid(),
                    "buildVersion",
                    $"Conflict: Log={logBuildVersion} vs Report={reportBuildVersion}",
                    EvidenceStatus.Conflict,
                    confidence: 0.5,
                    sources).Value;
            }
        }
        else if (sources.Count == 1)
        {
            var singleSource = sources[0];
            string value = singleSource.SourceType == EvidenceSourceType.Log ? logBuildVersion! : reportBuildVersion!;
            double confidence = singleSource.SourceType == EvidenceSourceType.Log ? 0.9 : 0.7;

            return EvidenceFact.Create(
                Guid.NewGuid(),
                "buildVersion",
                value,
                EvidenceStatus.Supported,
                confidence,
                sources).Value;
        }

        return EvidenceFact.Create(
            Guid.NewGuid(),
            "buildVersion",
            normalizedValue: null,
            EvidenceStatus.Unknown,
            confidence: 0.0,
            sources).Value;
    }

    private EvidenceFact ResolvePlatform(string? reportPlatform, string? logPlatform, string reportSourceRef, string logSourceRef)
    {
        var sources = new List<EvidenceSource>();

        if (!string.IsNullOrWhiteSpace(reportPlatform))
        {
            sources.Add(new(
                EvidenceSourceType.PlayerReport,
                reportSourceRef,
                lineStart: null,
                lineEnd: null,
                sanitizedExcerpt: $"Player reported platform: {reportPlatform}",
                excerptHash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reportPlatform))).ToLowerInvariant(),
                TrustLevel.UserStructured));
        }

        if (!string.IsNullOrWhiteSpace(logPlatform))
        {
            sources.Add(new(
                EvidenceSourceType.Log,
                logSourceRef,
                lineStart: null,
                lineEnd: null,
                sanitizedExcerpt: $"Log detected platform: {logPlatform}",
                excerptHash: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(logPlatform))).ToLowerInvariant(),
                TrustLevel.Observed));
        }

        if (sources.Count == 2)
        {
            if (string.Equals(reportPlatform, logPlatform, StringComparison.OrdinalIgnoreCase))
            {
                return EvidenceFact.Create(
                    Guid.NewGuid(),
                    "platform",
                    logPlatform,
                    EvidenceStatus.Corroborated,
                    confidence: 1.0,
                    sources).Value;
            }
            else
            {
                // Conflict
                return EvidenceFact.Create(
                    Guid.NewGuid(),
                    "platform",
                    $"Conflict: Log={logPlatform} vs Report={reportPlatform}",
                    EvidenceStatus.Conflict,
                    confidence: 0.5,
                    sources).Value;
            }
        }
        else if (sources.Count == 1)
        {
            var singleSource = sources[0];
            string value = singleSource.SourceType == EvidenceSourceType.Log ? logPlatform! : reportPlatform!;
            double confidence = singleSource.SourceType == EvidenceSourceType.Log ? 0.9 : 0.7;

            return EvidenceFact.Create(
                Guid.NewGuid(),
                "platform",
                value,
                EvidenceStatus.Supported,
                confidence,
                sources).Value;
        }

        return EvidenceFact.Create(
            Guid.NewGuid(),
            "platform",
            normalizedValue: null,
            EvidenceStatus.Unknown,
            confidence: 0.0,
            sources).Value;
    }
}
