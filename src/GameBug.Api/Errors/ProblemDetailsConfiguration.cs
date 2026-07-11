using System.Diagnostics;
using GameBug.Contracts.Errors;
using Microsoft.AspNetCore.Diagnostics;
using GameBug.Application.Abstractions.Files;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Api.Errors;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public System.Diagnostics.Activity? GetCurrentActivity()
    {
        return System.Diagnostics.Activity.Current;
    }

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        string traceId = GetCurrentActivity()?.Id ?? httpContext.TraceIdentifier;

        _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", traceId);

        var (status, code, title, retryable) = exception switch
        {
            FluentValidation.ValidationException => (
                StatusCodes.Status422UnprocessableEntity,
                "VALIDATION_FAILED",
                "Validation failed",
                false),

            BadHttpRequestException badReqEx when badReqEx.Message.Contains("Too large", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status413PayloadTooLarge,
                "PAYLOAD_TOO_LARGE",
                "Request payload too large",
                false),

            ObjectStorageException => (
                StatusCodes.Status503ServiceUnavailable,
                "STORAGE_FAILURE",
                "Object storage is temporarily unavailable",
                true),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "QA_REVIEW_VERSION_CONFLICT",
                "The requested resource was modified by another operation",
                false),

            DbUpdateException => (
                StatusCodes.Status503ServiceUnavailable,
                "DATABASE_FAILURE",
                "Database is temporarily unavailable",
                true),

            _ => (
                StatusCodes.Status500InternalServerError,
                "UNEXPECTED_ERROR",
                "An unexpected error occurred",
                false)
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        var errors = exception is FluentValidation.ValidationException valEx
            ? valEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            : null;

        var problem = new ProblemResponse(
            Type: $"https://gamebug/errors/{code.ToLowerInvariant()}",
            Title: title,
            Status: status,
            Code: code,
            Retryable: retryable,
            TraceId: traceId,
            Errors: errors);

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
