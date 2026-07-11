using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;

namespace GameBug.Application.ReproCases;

public class SeverityPolicy
{
    public (Severity Severity, string Reason) EstimateSeverity(
        IReadOnlyCollection<EvidenceFact> facts,
        Severity modelSeverity,
        string modelReason)
    {
        bool hasSupportedCrash = facts.Any(fact =>
            fact.FactType == "crashException" &&
            !string.IsNullOrWhiteSpace(fact.NormalizedValue) &&
            fact.Status is EvidenceStatus.Supported or EvidenceStatus.Corroborated);

        if (hasSupportedCrash)
        {
            return (Severity.High,
                $"[Policy Override] Crash exception detected in supported evidence; applying the High baseline. Original model reason: {modelReason}");
        }

        // Critical requires explicit evidence for data loss, security impact, or a payment
        // blocker. Keywords alone must never upgrade a result to Critical.
        return (modelSeverity, modelReason);
    }
}
