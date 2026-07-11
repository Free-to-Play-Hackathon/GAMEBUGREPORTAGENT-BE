using Microsoft.Extensions.Primitives;

namespace GameBug.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderKey = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out StringValues correlationId) ||
            !IsValid(correlationId.ToString()))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Response.Headers[CorrelationIdHeaderKey] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId.ToString() }))
        {
            await _next(context);
        }
    }

    private static bool IsValid(string value) =>
        value.Length is >= 1 and <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');
}
