using System.Diagnostics;
using GameBug.Application.Duplicates;
using GameBug.Application.HistoricalTickets.GetHistoricalTicket;
using GameBug.Application.HistoricalTickets.ImportHistoricalTickets;
using GameBug.Contracts.BugReports;
using GameBug.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameBug.Api.Endpoints.BugReports;

public static class HistoricalTicketEndpoints
{
    public static void MapHistoricalTicketEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/historical-tickets/import", async (
            ImportHistoricalTicketsRequest body,
            [FromServices] ISender sender,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            if (!request.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values))
            {
                return BugReportContractMapper.MapErrorToResult(
                    new DomainError("Validation.IdempotencyKeyRequired", "Idempotency-Key header is required."), traceId);
            }

            var command = new ImportHistoricalTicketsCommand(
                body.ProjectId ?? DuplicateSearchDocumentBuilder.DefaultProjectId,
                body.Source,
                body.ImportVersion,
                values.ToString(),
                body.Items.Select(i => new HistoricalTicketImportItem(
                    i.ExternalId,
                    i.Title,
                    i.Summary,
                    i.Status,
                    i.Severity,
                    i.BuildMin,
                    i.BuildMax,
                    i.Platforms,
                    i.StackSignature,
                    i.StackSummary,
                    i.GameEntities,
                    i.Symptom,
                    i.TriggerAction,
                    i.SceneOrFeature,
                    i.ActualResult,
                    i.SourceUpdatedAt)).ToArray());

            var result = await sender.Send(command, cancellationToken);
            if (result.IsFailure)
            {
                return BugReportContractMapper.MapErrorToResult(result.Error, traceId);
            }

            return Results.Accepted(
                $"/api/v1/admin/historical-ticket-imports/{result.Value.BatchId}",
                new ImportHistoricalTicketsResponse(
                    result.Value.BatchId,
                    result.Value.Status,
                    result.Value.AcceptedCount,
                    result.Value.RejectedCount,
                    result.Value.Errors.Select(e => new HistoricalTicketImportErrorResponse(e.ExternalId, e.Code, e.Message)).ToArray()));
        });

        app.MapGet("/api/v1/historical-tickets/{ticketId:guid}", async (
            Guid ticketId,
            [FromServices] ISender sender,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await sender.Send(new GetHistoricalTicketQuery(ticketId), cancellationToken);
            if (result.IsFailure)
            {
                return BugReportContractMapper.MapErrorToResult(result.Error, traceId);
            }

            var value = result.Value;
            return Results.Ok(new HistoricalTicketResponse(
                value.Id,
                value.ProjectId,
                value.Source,
                value.ExternalId,
                value.Title,
                value.Summary,
                value.Status,
                value.Severity,
                value.BuildMin,
                value.BuildMax,
                value.Platforms,
                value.StackSignature,
                value.StackSummary,
                value.GameEntities,
                value.Symptom,
                value.TriggerAction,
                value.SceneOrFeature,
                value.ActualResult,
                value.ImportVersion,
                value.IndexedAt));
        });
    }
}
