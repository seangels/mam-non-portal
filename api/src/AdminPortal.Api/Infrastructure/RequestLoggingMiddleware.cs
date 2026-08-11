using System.Diagnostics;

namespace AdminPortal.Api.Infrastructure;

public sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            LogRequest(
                logger,
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                context.TraceIdentifier);
        }
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms TraceId={TraceId}")]
    private static partial void LogRequest(
        ILogger logger,
        string method,
        string? path,
        int statusCode,
        double elapsedMilliseconds,
        string traceId);
}
