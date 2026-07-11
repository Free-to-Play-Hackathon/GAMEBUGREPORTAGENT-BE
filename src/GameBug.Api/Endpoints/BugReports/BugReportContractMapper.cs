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
            "BugReport.Forbidden" => (StatusCodes.Status403Forbidden, "FORBIDDEN", error.Description, false),
            "Idempotency.Conflict" => (StatusCodes.Status409Conflict, "IDEMPOTENCY_CONFLICT", error.Description, false),
            "Idempotency.Processing" => (StatusCodes.Status409Conflict, "IDEMPOTENCY_PROCESSING", error.Description, true),
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
