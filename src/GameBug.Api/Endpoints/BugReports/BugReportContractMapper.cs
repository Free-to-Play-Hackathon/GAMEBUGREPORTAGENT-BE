using GameBug.Application.BugReports.CreateReport;
using GameBug.Application.BugReports.GetReport;
using GameBug.Contracts.BugReports;
using GameBug.Contracts.Errors;
using GameBug.Domain.SharedKernel;

namespace GameBug.Api.Endpoints.BugReports;

public static class BugReportContractMapper
{
    public static CreateBugReportResponse MapToResponse(CreateReportResult result, string baseUrl)
    {
        return new CreateBugReportResponse(
            result.ReportId,
            result.Status.ToLowerInvariant(),
            result.AttachmentCount,
            result.CreatedAt,
            $"{baseUrl}/api/v1/bug-reports/{result.ReportId}");
    }

    public static BugReportResponse MapToResponse(GetReportResult result)
    {
        var attachments = result.Attachments.Select(a => new AttachmentResponse(
            a.AttachmentId,
            a.OriginalFileName,
            a.AttachmentType.ToLowerInvariant(),
            a.ContentType,
            a.SizeBytes,
            a.ScanStatus.ToLowerInvariant(),
            a.CreatedAt)).ToList();

        return new BugReportResponse(
            result.ReportId,
            result.Description,
            result.BuildVersion,
            result.Platform,
            result.Device,
            result.Locale,
            result.Status.ToLowerInvariant(),
            result.CreatedBy,
            result.CreatedAt,
            result.UpdatedAt,
            attachments);
    }

    public static IResult MapErrorToResult(DomainError error, string traceId)
    {
        var (status, code, title, retryable) = error.Code switch
        {
            "Auth.Unauthorized" => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", error.Description, false),
            "BugReport.NotFound" => (StatusCodes.Status404NotFound, "REPORT_NOT_FOUND", error.Description, false),
            "Analysis.NotFound" => (StatusCodes.Status404NotFound, "ANALYSIS_NOT_FOUND", error.Description, false),
            "BugReport.Forbidden" => (StatusCodes.Status403Forbidden, "FORBIDDEN", error.Description, false),
            "Idempotency.Conflict" or "Idempotency.KeyReused" => (StatusCodes.Status409Conflict, "IDEMPOTENCY_KEY_REUSED", error.Description, false),
            "Idempotency.Processing" => (StatusCodes.Status409Conflict, "IDEMPOTENCY_PROCESSING", error.Description, true),
            "Analysis.AlreadyInProgress" => (StatusCodes.Status409Conflict, "ANALYSIS_ALREADY_IN_PROGRESS", error.Description, false),
            "Analysis.ResultNotReady" => (StatusCodes.Status409Conflict, "ANALYSIS_RESULT_NOT_READY", error.Description, true),
            "Analysis.Failed" => (StatusCodes.Status409Conflict, "ANALYSIS_FAILED", error.Description, false),
            "Validation.IdempotencyKeyRequired" => (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", error.Description, false),
            "NO_SUPPORTED_TEXT_CONTENT" => (StatusCodes.Status422UnprocessableEntity, "NO_SUPPORTED_TEXT_CONTENT", error.Description, false),
            "UNSAFE_INPUT_REJECTED" => (StatusCodes.Status422UnprocessableEntity, "UNSAFE_INPUT_REJECTED", error.Description, false),
            "SANITIZER_FAILURE" => (StatusCodes.Status500InternalServerError, "SANITIZER_FAILURE", error.Description, false),
            "PROVIDER_TIMEOUT" => (StatusCodes.Status503ServiceUnavailable, "PROVIDER_TIMEOUT", error.Description, true),
            "PROVIDER_AUTH_FAILURE" => (StatusCodes.Status503ServiceUnavailable, "PROVIDER_AUTH_FAILURE", error.Description, false),
            "PROVIDER_FAILURE" => (StatusCodes.Status503ServiceUnavailable, "PROVIDER_FAILURE", error.Description, true),
            "INVALID_AI_SCHEMA" => (StatusCodes.Status502BadGateway, "INVALID_AI_SCHEMA", error.Description, false),
            "PROVENANCE_VALIDATION_FAILED" => (StatusCodes.Status502BadGateway, "PROVENANCE_VALIDATION_FAILED", error.Description, false),
            "ANALYSIS_FAILED" => (StatusCodes.Status500InternalServerError, "ANALYSIS_FAILED", error.Description, false),
            "BugReport.DescriptionRequired" or "BugReport.DescriptionInvalidLength" or "BugReport.CreatedByRequired" =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", error.Description, false),
            "BugReport.MaxAttachmentsExceeded" or "BugReport.AttachmentEmpty" or "BugReport.AttachmentTooLarge" or "BugReport.DuplicateAttachmentFileName" or "BugReport.InvalidImageFormat" or "BugReport.AttachmentMetadataMismatch" or "BugReport.StorageKeyContainsFileName" =>
                (StatusCodes.Status422UnprocessableEntity, "INVALID_FILE", error.Description, false),
            "Storage.Failure" => (StatusCodes.Status503ServiceUnavailable, "STORAGE_FAILURE", error.Description, true),
            "Database.Failure" => (StatusCodes.Status503ServiceUnavailable, "DATABASE_FAILURE", error.Description, true),
            _ => (StatusCodes.Status400BadRequest, "BAD_REQUEST", error.Description, false)
        };

        var problem = new ProblemResponse(
            Type: $"https://gamebug/errors/{code.ToLowerInvariant()}",
            Title: title,
            Status: status,
            Code: code,
            Retryable: retryable,
            TraceId: traceId);

        return Results.Json(problem, statusCode: status, contentType: "application/problem+json");
    }
}
