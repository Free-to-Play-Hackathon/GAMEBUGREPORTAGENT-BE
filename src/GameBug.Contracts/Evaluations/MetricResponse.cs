namespace GameBug.Contracts.Evaluations;

public sealed record MetricResponse(
    string Name,
    int Numerator,
    int Denominator,
    double? Value,
    string Unit,
    string Validity);
