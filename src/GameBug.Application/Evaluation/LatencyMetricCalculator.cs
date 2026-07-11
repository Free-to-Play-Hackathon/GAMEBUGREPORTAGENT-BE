using GameBug.Domain.Evaluation;

namespace GameBug.Application.Evaluation;

public sealed class LatencyMetricCalculator
{
    public MetricResult Calculate(
        IReadOnlyCollection<EvaluationCaseResult> caseResults,
        EvaluationValidity validity)
    {
        var successfulLatencies = caseResults
            .Where(c => c.Outcome == EvaluationCaseOutcome.Success && c.LatencyMs.HasValue)
            .Select(c => c.LatencyMs!.Value)
            .ToList();

        long total = successfulLatencies.Sum();
        double? average = successfulLatencies.Count == 0 ? null : successfulLatencies.Average();

        return MetricResult.Create(
            "EndToEndLatencyMs",
            total > int.MaxValue ? int.MaxValue : (int)total,
            successfulLatencies.Count,
            "ms",
            validity,
            average,
            enforceRatioBounds: false).Value;
    }
}
