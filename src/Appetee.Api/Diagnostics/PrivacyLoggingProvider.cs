using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace Appetee.Api.Diagnostics;

/// <summary>Sanitizes once before either stock console or Azure filesystem provider sees an event.</summary>
public sealed class PrivacyLoggingProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ILoggerFactory sinks;
    private readonly TelemetrySettings? metadata;
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

    public PrivacyLoggingProvider(ILoggerFactory sinks, TelemetrySettings? metadata = null)
        => (this.sinks, this.metadata) = (sinks, metadata);

    public static PrivacyLoggingProvider Create(IConfiguration configuration, TelemetrySettings? telemetry = null) => new(LoggerFactory.Create(logging =>
    {
        logging.AddConfiguration(configuration.GetSection("Logging"));
        logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.None);
        logging.AddConsole(options => options.FormatterName = SafeJsonConsoleFormatter.FormatterName);
        logging.AddConsoleFormatter<SafeJsonConsoleFormatter, ConsoleFormatterOptions>();
        logging.AddAzureWebAppDiagnostics();
        logging.Services.Configure<AzureFileLoggerOptions>(options =>
        {
            options.FileName = "appetee-diagnostics-";
            options.FileSizeLimit = 10 * 1024 * 1024;
            options.RetainedFileCountLimit = 5;
            options.BackgroundQueueSize = 1000;
            options.BatchSize = 32;
            options.FlushPeriod = TimeSpan.FromSeconds(1);
        });
        // Filesystem only for this slice: never activate a second Blob export implicitly.
        logging.AddFilter<BlobLoggerProvider>((_, _) => false);
        if (telemetry?.Enabled == true)
        {
            // This is the ONLY OpenTelemetry log provider: it sees projected state and null exceptions.
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = false;
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.SetResourceBuilder(telemetry.Resource());
                options.AddAzureMonitorLogExporter(exporter =>
                {
                    exporter.ConnectionString = telemetry.ConnectionString;
                    exporter.DisableOfflineStorage = true;
                    exporter.EnableLiveMetrics = false;
                    exporter.EnableTraceBasedLogsSampler = false;
                    exporter.Diagnostics.IsLoggingEnabled = false;
                    exporter.Diagnostics.IsDistributedTracingEnabled = false;
                });
            });
        }
    }), telemetry);

    public ILogger CreateLogger(string categoryName)
    {
        var category = SafeLogState.SafeCategory(categoryName);
        return new PrivacyLogger(this, category, sinks.CreateLogger(category));
    }
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;
    public void Dispose() => sinks.Dispose();

    private sealed class PrivacyLogger(PrivacyLoggingProvider provider, string category, ILogger sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider.scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => sink.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var safe = SafeLogState.Create(category, logLevel, eventId, state, exception, provider.scopes, provider.metadata);
            // Raw exceptions, state, formatter and scopes NEVER cross the privacy boundary.
            sink.Log(logLevel, new EventId(eventId.Id), safe, null, static (value, _) => value.ToString());
        }
    }
}

public sealed class SafeJsonConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "appetee-safe-json";
    public override void Write<TState>(in LogEntry<TState> entry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        if (entry.State is SafeLogState safe) textWriter.WriteLine(safe.ToString());
    }
}
