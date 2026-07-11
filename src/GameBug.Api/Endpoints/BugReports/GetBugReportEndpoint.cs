using System.Diagnostics;
using GameBug.Application.BugReports.GetReport;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameBug.Api.Endpoints.BugReports;

public static class GetBugReportEndpoint
{
    public static void MapGetBugReport(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/bug-reports/{reportId:guid}", async (
            Guid reportId,
            [FromServices] ISender sender,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;

            var query = new GetReportQuery(reportId);
            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
            {
                return BugReportContractMapper.MapErrorToResult(result.Error, traceId);
            }

            var response = BugReportContractMapper.MapToResponse(result.Value);
            return Results.Ok(response);
        });
    }
}
