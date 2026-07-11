using System.Diagnostics;
using GameBug.Api.Configuration;
using GameBug.Application.BugReports.CreateReport;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GameBug.Api.Endpoints.BugReports;

public static class CreateBugReportEndpoint
{
    public static void MapCreateBugReport(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/bug-reports", async (
            HttpRequest request,
            [FromServices] ISender sender,
            [FromServices] IOptions<IntakeOptions> intakeOptions,
            CancellationToken cancellationToken) =>
        {
            string traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;

            // 1. Extract Idempotency Key
            if (!request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeys) ||
                string.IsNullOrWhiteSpace(idempotencyKeys))
            {
                var problem = new Contracts.Errors.ProblemResponse(
                    Type: "https://gamebug/errors/validation_failed",
                    Title: "Idempotency-Key header is required.",
                    Status: StatusCodes.Status400BadRequest,
                    Code: "VALIDATION_FAILED",
                    Retryable: false,
                    TraceId: traceId);
                return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
            }

            string idempotencyKey = idempotencyKeys.ToString();

            // 2. Validate multipart
            if (!request.HasFormContentType)
            {
                var problem = new Contracts.Errors.ProblemResponse(
                    Type: "https://gamebug/errors/validation_failed",
                    Title: "Request must be multipart/form-data.",
                    Status: StatusCodes.Status400BadRequest,
                    Code: "VALIDATION_FAILED",
                    Retryable: false,
                    TraceId: traceId);
                return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
            }

            var form = await request.ReadFormAsync(cancellationToken);

            if (form.Files.Count > intakeOptions.Value.MaxFilesPerReport)
            {
                var problem = new Contracts.Errors.ProblemResponse(
                    Type: "https://gamebug/errors/invalid_file",
                    Title: $"Maximum of {intakeOptions.Value.MaxFilesPerReport} attachments is allowed.",
                    Status: StatusCodes.Status422UnprocessableEntity,
                    Code: "INVALID_FILE",
                    Retryable: false,
                    TraceId: traceId);
                return Results.Json(problem, statusCode: StatusCodes.Status422UnprocessableEntity, contentType: "application/problem+json");
            }

            string description = form["description"].ToString();
            string? buildVersion = form["buildVersion"].ToString();
            string? platform = form["platform"].ToString();
            string? device = form["device"].ToString();
            string? locale = form["locale"].ToString();
            string? sessionReference = form["sessionReference"].ToString();

            // Convert string values to null if empty
            buildVersion = string.IsNullOrWhiteSpace(buildVersion) ? null : buildVersion;
            platform = string.IsNullOrWhiteSpace(platform) ? null : platform;
            device = string.IsNullOrWhiteSpace(device) ? null : device;
            locale = string.IsNullOrWhiteSpace(locale) ? null : locale;
            sessionReference = string.IsNullOrWhiteSpace(sessionReference) ? null : sessionReference;

            var attachments = new List<FileAttachmentCommand>();
            foreach (var file in form.Files)
            {
                // We wrap file.OpenReadStream() which streams data
                attachments.Add(new FileAttachmentCommand(
                    file.FileName,
                    file.ContentType,
                    file.OpenReadStream()
                ));
            }

            var command = new CreateReportCommand(
                description,
                buildVersion,
                platform,
                device,
                locale,
                sessionReference,
                idempotencyKey,
                attachments);

            try
            {
                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    return BugReportContractMapper.MapErrorToResult(result.Error, traceId);
                }

                string baseUrl = $"{request.Scheme}://{request.Host}";
                var response = BugReportContractMapper.MapToResponse(result.Value, baseUrl);

                return Results.Created(response.ResourceUrl, response);
            }
            finally
            {
                foreach (var attachment in attachments)
                {
                    await attachment.ContentStream.DisposeAsync();
                }
            }
        })
        .DisableAntiforgery(); // Required for multipart testing in Minimal APIs if not using CSRF tokens
    }
}
