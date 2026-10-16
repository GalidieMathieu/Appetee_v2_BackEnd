using System.Diagnostics;
using OpenTelemetry;

namespace Appetee.Api.Diagnostics;

/// <summary>Runs BEFORE the distro exporters. Default instrumentation is not a privacy boundary.</summary>
public sealed class SafeActivityProcessor : BaseProcessor<Activity>
{
    internal const string RequestContextKey = "Appetee.SafeRequestContext";

    public override void OnEnd(Activity activity)
    {
        var source = activity.Source.Name;
        var server = source == "Microsoft.AspNetCore";
        var http = source == "System.Net.Http";
        var blob = source == "Azure.Storage.Blobs";
        var azureHttp = source == "Azure.Core.Http";
        // Unreviewed sources (including SQL) and event/link payloads must never reach export.
        // Events/links are immutable through Activity's public API: drop, never regex-scrub them.
        if ((!server && !http && !blob && !azureHttp) || activity.Events.Any() || activity.Links.Any())
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            return;
        }
        var context = server ? activity.GetCustomProperty(RequestContextKey) as RequestLogContext : null;
        var method = context?.HttpMethod ?? SafeMethod(activity.GetTagItem("http.request.method") as string
            ?? activity.GetTagItem("http.method") as string);
        var status = activity.GetTagItem("http.response.status_code") ?? activity.GetTagItem("http.status_code");
        foreach (var tag in activity.TagObjects.ToArray()) activity.SetTag(tag.Key, null);
        foreach (var baggage in activity.Baggage.ToArray()) activity.SetBaggage(baggage.Key, null);
        activity.TraceStateString = null;
        activity.SetStatus(activity.Status); // Remove status descriptions, which can be raw error messages.
        activity.DisplayName = server ? method + " " + (context?.Route ?? "<unmatched>")
            : http ? "HTTP " + method : blob ? "Blob operation" : "Azure HTTP";
        activity.SetTag("http.request.method", method);
        if (context is not null) activity.SetTag("http.route", context.Route);
        if (status is int code && code is >= 100 and <= 599) activity.SetTag("http.response.status_code", code);
        else if (status is long longCode && longCode is >= 100 and <= 599) activity.SetTag("http.response.status_code", longCode);
        activity.SetTag("appetee.dependency", server ? "API" : http ? "HTTP" : blob ? "Blob" : "Azure");
    }

    private static string SafeMethod(string? method) => method is "GET" or "HEAD" or "OPTIONS" or "POST" or "PUT" or "PATCH" or "DELETE" ? method : "OTHER";
}
