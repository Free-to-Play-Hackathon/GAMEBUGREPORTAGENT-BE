using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Evaluation;

public record MetricResult(
    string Name,
    int Numerator,
    int Denominator,
    double? Value,
    string Unit,
    EvaluationValidity Validity)
{
    public static Result<MetricResult> Create(
        string name,
        int numerator,
        int denominator,
        string unit,
        EvaluationValidity validity,
        double? value = null,
        bool enforceRatioBounds = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<MetricResult>(new DomainError("MetricResult.NameRequired", "Metric name is required."));
        }

        if (numerator < 0 || denominator < 0)
        {
            return Result.Failure<MetricResult>(new DomainError("MetricResult.InvalidCounts", "Metric counts cannot be negative."));
        }

        if (enforceRatioBounds && numerator > denominator && denominator > 0)
        {
            return Result.Failure<MetricResult>(new DomainError("MetricResult.InvalidRatio", "Metric numerator cannot exceed denominator."));
        }

        double? computed = denominator == 0 ? null : value ?? (double)numerator / denominator;
        return new MetricResult(name.Trim(), numerator, denominator, computed, unit.Trim(), validity);
    }
}
