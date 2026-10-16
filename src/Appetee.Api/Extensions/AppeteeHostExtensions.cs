/*
 * Purpose: Defines the high-level backend composition used by the minimal Program.cs entry point.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T22:38:10-06:00
 */

using Azure.Identity;
using Appetee.Api.Diagnostics;

namespace Appetee.Api.Extensions;

/// <summary>Composes logging and dependency-registration modules for the Appetee API host.</summary>
internal static class AppeteeHostExtensions
{
    public static WebApplicationBuilder AddAppeteeBackend(
        this WebApplicationBuilder builder)
    {
        // Every sink receives only the allowlisted projection, including framework/SDK events.
        builder.Logging.ClearProviders();
        // The central handler records the exception once; request completion is a separate event.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);

        builder.AddDevelopmentKeyVault();

        var telemetry = builder.Services.AddAppeteeTelemetry(builder.Configuration, builder.Environment);
        // UseAzureMonitor registers an outer log provider by default. Remove it: every log sink
        // must sit behind the existing allowlist, not beside it. Trace/metric providers remain.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(PrivacyLoggingProvider.Create(builder.Configuration, telemetry));

        builder.Services
            .AddAppeteeApi(builder.Configuration, builder.Environment)
            .AddAppeteeInfrastructure(builder.Configuration, builder.Environment)
            .AddAppeteeAuthentication(builder.Configuration, builder.Environment)
            .AddAppeteeApplication();

        return builder;
    }

    private static void AddDevelopmentKeyVault(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()
            || !builder.Configuration.GetValue("KeyVault:Enabled", true))
        {
            return;
        }

        var configuredUri = builder.Configuration["KeyVault:Uri"];
        if (string.IsNullOrWhiteSpace(configuredUri))
        {
            return;
        }

        if (!Uri.TryCreate(configuredUri, UriKind.Absolute, out var keyVaultUri)
            || keyVaultUri.Scheme != Uri.UriSchemeHttps
            || !keyVaultUri.Host.EndsWith(".vault.azure.net", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(keyVaultUri.UserInfo)
            || !string.IsNullOrEmpty(keyVaultUri.Query)
            || !string.IsNullOrEmpty(keyVaultUri.Fragment))
        {
            throw new InvalidOperationException(
                "KeyVault:Uri must be an absolute HTTPS Azure Key Vault URL.");
        }

        // Local development uses the signed-in Azure identity; App Service resolves its own Key Vault references.
        builder.Configuration.AddAzureKeyVault(
            keyVaultUri,
            new DefaultAzureCredential());
    }
}
