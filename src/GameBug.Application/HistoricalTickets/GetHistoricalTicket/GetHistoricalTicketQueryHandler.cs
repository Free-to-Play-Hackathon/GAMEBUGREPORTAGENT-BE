using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.HistoricalTickets.GetHistoricalTicket;

public sealed class GetHistoricalTicketQueryHandler : IRequestHandler<GetHistoricalTicketQuery, Result<HistoricalTicketDetails>>
{
    private readonly IHistoricalTicketRepository _tickets;
    private readonly ICurrentUser _currentUser;

    public GetHistoricalTicketQueryHandler(IHistoricalTicketRepository tickets, ICurrentUser currentUser)
    {
        _tickets = tickets;
        _currentUser = currentUser;
    }

    public async Task<Result<HistoricalTicketDetails>> Handle(GetHistoricalTicketQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure<HistoricalTicketDetails>(new DomainError("HistoricalTicket.NotFound", "The historical ticket was not found."));
        }

        var ticket = await _tickets.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<HistoricalTicketDetails>(new DomainError("HistoricalTicket.NotFound", "The historical ticket was not found."));
        }

        return new HistoricalTicketDetails(
            ticket.Id,
            ticket.ProjectId,
            ticket.Source,
            ticket.ExternalId,
            ticket.Title,
            ticket.SummarySanitized,
            ticket.Status,
            ticket.Severity,
            ticket.BuildMin,
            ticket.BuildMax,
            ticket.Platforms,
            ticket.StackSignature,
            ticket.StackSummary,
            ticket.GameEntities,
            ticket.Symptom,
            ticket.TriggerAction,
            ticket.SceneOrFeature,
            ticket.ActualResult,
            ticket.ImportVersion,
            ticket.IndexedAt);
    }
}
