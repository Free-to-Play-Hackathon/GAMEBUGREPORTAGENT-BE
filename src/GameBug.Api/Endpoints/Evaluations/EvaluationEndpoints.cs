using System.Diagnostics;
using GameBug.Application.Evaluation.ExportEvaluation;
using GameBug.Application.Evaluation.GetEvaluation;
using GameBug.Application.Evaluation.RunEvaluation;
using GameBug.Contracts.Evaluations;
using GameBug.Api.Endpoints.BugReports;
using GameBug.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameBug.Api.Endpoints.Evaluations;

public static class EvaluationEndpoints
{
    public static void MapEvaluationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/evaluations")
            .AddEndpointFilter(async (context, next) =>
            {
                var env = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
                if (!env.IsEnvironment("Local") && !env.IsEnvironment("Demo") && !env.IsEnvironment("Test") && !env.IsDevelopment())
                {
                    return Results.Forbid();
                }

                return await next(context);
            });

        group.MapPost("/", async (
            StartEvaluationRequest body,
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

            var result = await sender.Send(new RunEvaluationCommand(
                body.ManifestId,
                body.Profile,
                values.ToString()), cancellationToken);

            return result.IsFailure
                ? BugReportContractMapper.MapErrorToResult(result.Error, traceId)
                : Results.Accepted($"/api/v1/evaluations/{result.Value}", new { RunId = result.Value });
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ISender sender,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await sender.Send(new GetEvaluationQuery(id), cancellationToken);
            return result.IsFailure
                ? BugReportContractMapper.MapErrorToResult(result.Error, traceId)
                : Results.Ok(result.Value);
        });

        group.MapGet("/{id:guid}/artifact", async (
            Guid id,
            [FromServices] ISender sender,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var result = await sender.Send(new ExportEvaluationQuery(id), cancellationToken);
            if (result.IsFailure)
            {
                return BugReportContractMapper.MapErrorToResult(result.Error, traceId);
            }

            string path = result.Value;
            return File.Exists(path)
                ? Results.File(path, "application/json", Path.GetFileName(path))
                : BugReportContractMapper.MapErrorToResult(
                    new DomainError("Evaluation.ArtifactMissing", "Evaluation artifact could not be found."), traceId);
        });
    }
}
