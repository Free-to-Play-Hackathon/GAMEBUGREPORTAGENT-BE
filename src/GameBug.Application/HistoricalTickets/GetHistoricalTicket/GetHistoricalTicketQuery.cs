using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.HistoricalTickets.GetHistoricalTicket;

public sealed record GetHistoricalTicketQuery(Guid TicketId) : IRequest<Result<HistoricalTicketDetails>>;

public sealed record HistoricalTicketDetails(
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
