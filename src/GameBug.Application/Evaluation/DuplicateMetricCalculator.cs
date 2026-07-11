using GameBug.Domain.Evaluation;

namespace GameBug.Application.Evaluation;

public sealed class DuplicateMetricCalculator
{
    public IReadOnlyList<MetricResult> Calculate(
        IReadOnlyCollection<EvaluationCaseResult> caseResults,
        IReadOnlyCollection<EvaluationManifestCase> manifestCases,
        IReadOnlyCollection<EvaluationGroundTruthEntry> groundTruthEntries,
        EvaluationValidity validity)
    {
        var caseTypes = manifestCases.ToDictionary(c => c.CaseId, c => c.Type, StringComparer.OrdinalIgnoreCase);
        var results = caseResults.ToDictionary(c => c.CaseId, StringComparer.OrdinalIgnoreCase);
        var duplicateTruth = groundTruthEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.ExpectedDuplicateKey))
            .ToList();

        int duplicateDenominator = duplicateTruth.Count;
        int recallAt1 = 0;
        int recallAt3 = 0;
        double reciprocalRankSum = 0;

        foreach (var entry in duplicateTruth)
        {
            if (!results.TryGetValue(entry.CaseId, out var result))
            {
                continue;
            }

            if (result.ActualRank == 1)
            {
                recallAt1++;
            }

            if (result.ActualRank is >= 1 and <= 3)
            {
                recallAt3++;
                reciprocalRankSum += 1d / result.ActualRank.Value;
            }
        }

        var hardNegativeCases = manifestCases
            .Where(c => c.Type.Equals("HardNegative", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.CaseId)
            .ToList();
        int hardNegativeFp = hardNegativeCases.Count(caseId =>
            results.TryGetValue(caseId, out var result) &&
            string.Equals(result.ActualClassification, "LikelyDuplicate", StringComparison.OrdinalIgnoreCase));

        return new[]
        {
            MetricResult.Create("DuplicateRecallAt1", recallAt1, duplicateDenominator, "ratio", validity).Value,
            MetricResult.Create("DuplicateRecallAt3", recallAt3, duplicateDenominator, "ratio", validity).Value,
            MetricResult.Create(
                "MeanReciprocalRank",
                (int)Math.Round(reciprocalRankSum * 1000),
                duplicateDenominator,
                "ratio",
                validity,
                duplicateDenominator == 0 ? null : reciprocalRankSum / duplicateDenominator,
                enforceRatioBounds: false).Value,
            MetricResult.Create("HardNegativeFpRate", hardNegativeFp, hardNegativeCases.Count, "ratio", validity).Value
        };
    }
}
