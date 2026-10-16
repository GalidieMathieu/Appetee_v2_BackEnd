using Appetee.Api.Extensions;
using Appetee.Api.Diagnostics;

PrivacyLoggingProvider? startupLogger = null;
try
{
    // Available before application DI, Key Vault and options validation can fail.
    startupLogger = PrivacyLoggingProvider.Create(new ConfigurationBuilder().AddEnvironmentVariables().Build());
    var builder = WebApplication.CreateBuilder(args);
    builder.AddAppeteeBackend();

    var app = builder.Build();
    app.UseAppeteePipeline();
    app.Run();
}
catch (HostAbortedException)
{
    // Tooling (including WebApplicationFactory) uses this to stop host discovery.
    throw;
}
catch (Exception ex)
{
    // The managed logger may not exist yet. Do not let the runtime print raw exception messages.
    StartupDiagnostics.ReportFailure(ex, startupLogger, Console.Error);
    Environment.ExitCode = 1;
}
finally
{
    try { startupLogger?.Dispose(); }
    catch (Exception exception) { StartupDiagnostics.WriteFailure(exception, Console.Error); }
}

// Expose Program for WebApplicationFactory integration tests.
public partial class Program
{
}
