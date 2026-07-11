using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.ReproCases;

public record ConfidenceScore
{
    // For EF Core
    private ConfidenceScore() { }

    private ConfidenceScore(double value)
    {
        Value = value;
    }

    public double Value { get; private set; }

    public static Result<ConfidenceScore> Create(double value)
    {
        if (value < 0.0 || value > 1.0)
        {
            return Result.Failure<ConfidenceScore>(new DomainError("ConfidenceScore.InvalidValue", "Confidence score must be between 0.0 and 1.0."));
        }

        return new ConfidenceScore(value);
    }
}
