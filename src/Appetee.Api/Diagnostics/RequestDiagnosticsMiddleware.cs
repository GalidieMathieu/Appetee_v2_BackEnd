using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Appetee.Api.Diagnostics;

public sealed class RequestLogContext
{
    public string TraceId { get; } = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
    public string? SpanId { get; } = Activity.Current?.SpanId.ToHexString();
    public string HttpMethod { get; }
    public string Route { get; private set; } = "<unmatched>";
    public RequestLogContext(string method) => HttpMethod = method is "GET" or "HEAD" or "OPTIONS" or "POST" or "PUT" or "PATCH" or "DELETE" ? method : "OTHER";
    public void CaptureEndpoint(HttpContext context)
    {
        if (Route == "<unmatched>" && context.GetEndpoint() is RouteEndpoint endpoint
            && endpoint.RoutePattern.RawText is { Length: <= 256 } route)
            Route = route.StartsWith('/') ? route : "/" + route;
    }
}

public sealed class RequestDiagnosticsMiddleware(RequestDelegate next, ILogger<RequestDiagnosticsMiddleware> logger)
{
    public static readonly object ContextKey = new();
    public const string TraceHeader = "X-Trace-Id";
    public const string AppeteeTraceHeader = "X-Appetee-Trace-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var request = new RequestLogContext(context.Request.Method);
        context.Items[ContextKey] = request;
        Activity.Current?.SetCustomProperty(SafeActivityProcessor.RequestContextKey, request);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[TraceHeader] = request.TraceId;
            context.Response.Headers[AppeteeTraceHeader] = request.TraceId;
            return Task.CompletedTask;
        });
        var started = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(request);
        var status = 500;
        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            status = 499;
            logger.LogInformation(new EventId(1002), "Request cancelled. StatusCode {StatusCode}; ElapsedMs {ElapsedMs}",
                499, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            // Last-resort failure when the normal handler itself cannot finish (e.g. response already started).
            logger.LogError(new EventId(1001), exception, "Request failed outside the error handler. ResponseStarted {ResponseStarted}", context.Response.HasStarted);
            throw;
        }
        finally
        {
            var level = status >= 500 ? LogLevel.Error : status >= 400 && status != 499 ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level, new EventId(1000), "Request completed. StatusCode {StatusCode}; ResponseStatusCode {ResponseStatusCode}; ResponseStarted {ResponseStarted}; ElapsedMs {ElapsedMs}",
                status, context.Response.StatusCode, context.Response.HasStarted, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
