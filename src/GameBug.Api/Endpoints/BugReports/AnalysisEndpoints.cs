using System.Diagnostics;
using GameBug.Application.Analysis.CancelAnalysis;
using GameBug.Application.Analysis.GetAnalysis;
using GameBug.Application.Analysis.GetAnalysisResult;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Contracts.BugReports;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameBug.Api.Endpoints.BugReports;

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/bug-reports/{reportId:guid}/analyses", async (
            Guid reportId,
            StartAnalysisRequest body,
            [FromServices] ISender sender,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            if (!request.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values))
            {
                return BugReportContractMapper.MapErrorToResult(
                    new Domain.SharedKernel.DomainError("Validation.IdempotencyKeyRequired", "Idempotency-Key header is required."), traceId);
            }

            var result = await sender.Send(new StartAnalysisCommand(
                reportId, values.ToString(), body.RequestedSchemaVersion, body.ConfigurationProfile), cancellationToken);
            if (result.IsFailure)
            {
                return BugReportContractMapper.MapErrorToResult(result.Error, traceId);
            }

            var value = result.Value;
            return Results.Accepted(value.StatusUrl, new StartAnalysisResponse(
                value.AnalysisId, value.ReportId, value.Version, value.Status, value.StatusUrl, value.ResultUrl));
        });

        app.MapGet("/api/v1/analyses/{analysisId:guid}", async (
            Guid analysisId, [FromServices] ISender sender, HttpRequest request, CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await sender.Send(new GetAnalysisQuery(analysisId), cancellationToken);
            return result.IsFailure
                ? BugReportContractMapper.MapErrorToResult(result.Error, traceId)
                : Results.Ok(result.Value);
        });

        app.MapGet("/api/v1/analyses/{analysisId:guid}/result", async (
            Guid analysisId, [FromServices] ISender sender, HttpRequest request, CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await sender.Send(new GetAnalysisResultQuery(analysisId), cancellationToken);
            return result.IsFailure
                ? BugReportContractMapper.MapErrorToResult(result.Error, traceId)
                : Results.Ok(result.Value);
        });

        app.MapPost("/api/v1/analyses/{analysisId:guid}/cancel", async (
            Guid analysisId, [FromServices] ISender sender, HttpRequest request, CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await sender.Send(new CancelAnalysisCommand(analysisId), cancellationToken);
            return result.IsFailure
                ? BugReportContractMapper.MapErrorToResult(result.Error, traceId)
                : Results.Accepted($"/api/v1/analyses/{analysisId}", result.Value);
        });
    }
}
