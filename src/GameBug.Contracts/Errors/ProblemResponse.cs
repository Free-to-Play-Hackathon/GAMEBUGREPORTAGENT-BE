namespace GameBug.Contracts.Errors;

public record ProblemResponse(
    string Type,
    string Title,
    int Status,
    string Code,
    bool Retryable,
    string TraceId,
    IReadOnlyDictionary<string, string[]>? Errors = null);
