namespace GameBug.Contracts.BugReports;

public sealed record ImportHistoricalTicketsRequest(
    Guid? ProjectId,
    string Source,
    string ImportVersion,
    IReadOnlyList<HistoricalTicketImportItemRequest> Items);

public sealed record HistoricalTicketImportItemRequest(
    string ExternalId,
    string Title,
    string Summary,
    string Status,
    string Severity,
    string? BuildMin,
    string? BuildMax,
    IReadOnlyList<string> Platforms,
    string? StackSignature,
    string? StackSummary,
    IReadOnlyList<string> GameEntities,
    string? Symptom,
    string? TriggerAction,
    string? SceneOrFeature,
    string? ActualResult,
    DateTimeOffset? SourceUpdatedAt);

public sealed record ImportHistoricalTicketsResponse(
    Guid BatchId,
    string Status,
    int AcceptedCount,
    int RejectedCount,
    IReadOnlyList<HistoricalTicketImportErrorResponse> Errors);

public sealed record HistoricalTicketImportErrorResponse(string ExternalId, string Code, string Message);

public sealed record HistoricalTicketResponse(
    Guid Id,
    Guid ProjectId,
    string Source,
    string ExternalId,
    string Title,
    string Summary,
    string Status,
    string Severity,
    string? BuildMin,
    string? BuildMax,
    IReadOnlyList<string> Platforms,
    string? StackSignature,
    string? StackSummary,
    IReadOnlyList<string> GameEntities,
    string? Symptom,
    string? TriggerAction,
    string? SceneOrFeature,
    string? ActualResult,
    string ImportVersion,
    DateTimeOffset? IndexedAt);
