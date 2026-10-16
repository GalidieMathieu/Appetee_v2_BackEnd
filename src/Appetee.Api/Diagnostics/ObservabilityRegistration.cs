using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Resources;

namespace Appetee.Api.Diagnostics;

internal static class ObservabilityRegistration
{
    public static TelemetrySettings AddAppeteeTelemetry(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = TelemetrySettings.Read(configuration, environment);
        services.AddSingleton(settings);
        if (!settings.Enabled) return settings;

        // The distro otherwise disables query redaction by default. Enforce it in its configuration.
        configuration["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION"] = "false";
        configuration["OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_DISABLE_URL_QUERY_REDACTION"] = "false";
        services.AddOpenTelemetry()
            // Registration order is security-critical: sanitize BEFORE every distro batch exporter.
            .WithTracing(tracing => tracing.AddProcessor(new SafeActivityProcessor()))
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = settings.ConnectionString;
                options.EnableLiveMetrics = false;
                options.EnableStandardMetrics = false;
                options.EnablePerformanceCounters = false;
                options.DisableOfflineStorage = true;
                options.Diagnostics.IsLoggingEnabled = false;
                options.Diagnostics.IsDistributedTracingEnabled = false;
                options.EnableTraceBasedLogsSampler = false;
                // Explicit initial 100% sampling; no silent 5 traces/sec distro default.
                options.TracesPerSecond = null;
                options.SamplingRatio = 1.0F;
            })
            .ConfigureResource(resource => resource.Clear().AddAttributes(settings.Resource().Build().Attributes))
            .WithMetrics(metrics => metrics.AddMeter("System.Runtime")
                // Do not export arbitrary metric tags (paths, hosts, error descriptions, connection targets).
                .AddView(instrument => ApprovedMetric(instrument.Meter.Name, instrument.Name)
                    ? new MetricStreamConfiguration { TagKeys = [] } : MetricStreamConfiguration.Drop));
        services.Configure<AspNetCoreTraceInstrumentationOptions>(options => options.RecordException = false);
        services.Configure<HttpClientTraceInstrumentationOptions>(options => options.RecordException = false);
        // The default Azure SDK ActivitySource is opt-in; payloads are filtered by SafeActivityProcessor.
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
        return settings;
    }

    internal static bool ApprovedMetric(string meter, string instrument) =>
        meter == "System.Runtime" || (meter == "Microsoft.AspNetCore.Hosting" && instrument is "http.server.request.duration" or "http.server.active_requests")
        || (meter == "System.Net.Http" && instrument is "http.client.request.duration" or "http.client.active_requests");
}
