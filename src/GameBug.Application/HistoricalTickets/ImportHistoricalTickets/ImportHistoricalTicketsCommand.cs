using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.HistoricalTickets.ImportHistoricalTickets;

public sealed record ImportHistoricalTicketsCommand(
    Guid ProjectId,
    string Source,
    string ImportVersion,
    string IdempotencyKey,
    IReadOnlyList<HistoricalTicketImportItem> Items) : IRequest<Result<ImportHistoricalTicketsResult>>;

public sealed record HistoricalTicketImportItem(
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

public sealed record ImportHistoricalTicketsResult(
    Guid BatchId,
    string Status,
    int AcceptedCount,
    int RejectedCount,
    IReadOnlyList<HistoricalTicketImportError> Errors);

public sealed record HistoricalTicketImportError(string ExternalId, string Code, string Message);
