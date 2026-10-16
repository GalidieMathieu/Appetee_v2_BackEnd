using System.Reflection;
using OpenTelemetry.Resources;

namespace Appetee.Api.Diagnostics;

/// <summary>Explicit opt-in; never formats configuration or connection settings into errors.</summary>
public sealed class TelemetrySettings
{
    public bool Enabled { get; }
    internal string? ConnectionString { get; }
    public string Environment { get; }
    public string ReleaseVersion { get; }

    private TelemetrySettings(bool enabled, string? connectionString, string environment, string release)
        => (Enabled, ConnectionString, Environment, ReleaseVersion) = (enabled, connectionString, environment, release);

    public static TelemetrySettings Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = configuration.GetValue("Observability:Enabled", false);
        var release = configuration["Observability:ReleaseVersion"]
            ?? typeof(TelemetrySettings).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
        if (release.Length is 0 or > 128 || !release.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '+'))
            throw new InvalidOperationException("Observability release metadata must be a bounded build/version identifier.");
        var stage = environment.IsProduction() ? "Production" : environment.IsStaging() ? "Staging" : "Development";
        var connectionString = enabled ? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] : null;
        if (enabled && string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Explicit telemetry export requires Application Insights configuration.");
        if (enabled && stage != "Development" && string.IsNullOrWhiteSpace(configuration["Observability:ReleaseVersion"]))
            throw new InvalidOperationException("Non-development telemetry export requires an explicit deployed release identifier.");
        // Development requires an additional explicit opt-in and a separately reviewed non-production resource.
        if (enabled && stage == "Development" && !configuration.GetValue("Observability:AllowDevelopmentExport", false))
            throw new InvalidOperationException("Development telemetry export requires explicit non-production approval.");
        return new(enabled, connectionString, stage, release);
    }

    public ResourceBuilder Resource() => ResourceBuilder.CreateEmpty()
        .AddService("appetee-api", serviceNamespace: "appetee", serviceVersion: ReleaseVersion,
            autoGenerateServiceInstanceId: false)
        .AddAttributes(new Dictionary<string, object> { ["deployment.environment"] = Environment });
}
