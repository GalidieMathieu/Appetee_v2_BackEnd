namespace Appetee.Api.Diagnostics;

public static class StartupDiagnostics
{
    public static void ReportFailure(Exception exception, ILoggerProvider? provider, TextWriter fallback)
    {
        try
        {
            if (provider is not null)
            {
                var logger = provider.CreateLogger("Appetee.Startup");
                if (logger.IsEnabled(LogLevel.Critical))
                {
                    logger.LogCritical(Appetee.Application.Diagnostics.LogEvents.StartupFailed, exception, "Application startup failed. ExitCode {ExitCode}", 1);
                    return;
                }
            }
        }
        catch { /* A failing sink must not hide startup diagnostics or expose its raw error. */ }
        WriteFailure(exception, fallback);
    }

    public static void WriteFailure(Exception exception, TextWriter writer)
    {
        var safe = SafeLogState.Create("Appetee.Startup", LogLevel.Critical, new EventId(1199),
            new KeyValuePair<string, object?>[] { new("ExitCode", 1) }, exception);
        writer.WriteLine(safe.ToString());
        writer.Flush();
    }
}
