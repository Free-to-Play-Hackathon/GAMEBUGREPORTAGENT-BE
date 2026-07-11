using System.Diagnostics;

namespace GameBug.Api.Middleware;

public sealed class SafeRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public SafeRequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<SafeRequestLoggingMiddleware> logger)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            double durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            logger.LogInformation(
                "HTTP {RequestMethod} {Route} returned {StatusCode} in {DurationMs:F1} ms",
                context.Request.Method,
                context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value,
                context.Response.StatusCode,
                durationMs);
        }
    }
}
