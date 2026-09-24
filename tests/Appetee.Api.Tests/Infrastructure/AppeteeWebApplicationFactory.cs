/*
 * Purpose: Hosts the backend with isolated MySQL, Blob, email, and cryptographic test dependencies.
 * Change reason: Expose and reset controllable Blob behavior for E-001 Phase 4 fault tests.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Appetee.Infrastructure.Data;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Appetee.Api.Tests.Infrastructure;

public sealed class AppeteeWebApplicationFactory : WebApplicationFactory<Program>
{
    internal TestBlobStorageService BlobStorage { get; } = new();

    internal TestPasswordRecoveryEmailSender RecoveryEmailSender { get; } = new();

    internal ApiTestDatabase Database { get; } = new();

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // The Key Vault provider loads immediately, so integration tests disable it before Program runs.
        builder.UseSetting("KeyVault:Enabled", "false");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppeteeDb"] = Database.ConnectionString,
                ["ConnectionStrings:Default"] = Database.ConnectionString,
                ["AzureStorage:AccountUrl"] = "https://test.blob.core.windows.net",
                ["AzureStorage:ContainerName"] = "test-images",
                ["CommunicationEmail:Endpoint"] = "https://test.communication.azure.com",
                ["CommunicationEmail:AccessKey"] = "integration-test-placeholder-key",
                ["CommunicationEmail:FromAddress"] = "DoNotReply@test.azurecomm.net",
            });
        });

        builder.ConfigureLogging(logging =>
        {
            // EventLog is unavailable in the test host, so keep logging in-memory only.
            logging.ClearProviders();
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbConnectionFactory>();
            services.RemoveAll<IBlobStorageService>();
            services.RemoveAll<BlobServiceClient>();
            services.RemoveAll<IPasswordRecoveryEmailSender>();

            var keyDirectory = Path.Combine(AppContext.BaseDirectory, "data-protection-keys");
            Directory.CreateDirectory(keyDirectory);

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
                .SetApplicationName("Appetee.Api.Tests");

            services.AddScoped<IDbConnectionFactory>(_ => new DbConnectionFactory(Database.ConnectionString));
            services.AddSingleton<IBlobStorageService>(BlobStorage);
            services.AddSingleton<IPasswordRecoveryEmailSender>(RecoveryEmailSender);
        });
    }
}
