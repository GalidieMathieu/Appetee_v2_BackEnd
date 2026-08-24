/*
 * Purpose: Registers database, Azure Blob Storage, and repository adapters outside Program.cs.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T01:18:52-06:00
 */

using Appetee.Application.Abstractions.Diets;
using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Abstractions.Users;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Diets;
using Appetee.Infrastructure.Ingredients;
using Appetee.Infrastructure.Recipes;
using Appetee.Infrastructure.Users;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace Appetee.Api.Extensions;

/// <summary>Provides infrastructure registrations and validates their required deployment configuration.</summary>
internal static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAppeteeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var storageAccountUri = GetRequiredAzureStorageAccountUri(
            configuration,
            "AzureStorage:AccountUrl");
        GetRequiredConfigurationValue(
            configuration,
            "AzureStorage:ContainerName");
        var connectionString = GetRequiredConnectionString(configuration);

        services.AddSingleton(_ =>
        {
            var credential = new DefaultAzureCredential(
                new DefaultAzureCredentialOptions
                {
                    // Production hosts must never initiate interactive authentication.
                    ExcludeInteractiveBrowserCredential = !environment.IsDevelopment(),
                });

            return new BlobServiceClient(storageAccountUri, credential);
        });

        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IDbConnectionFactory>(
            _ => new DbConnectionFactory(connectionString));
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDietQueries, DietQueries>();
        services.AddScoped<IIngredientQueries, IngredientQueries>();
        services.AddScoped<IRecipeQueries, RecipeQueries>();

        return services;
    }

    private static Uri GetRequiredAzureStorageAccountUri(
        IConfiguration configuration,
        string key)
    {
        var value = GetRequiredConfigurationValue(configuration, key);

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                "Invalid Azure Storage account URL.");
        }

        var isHttps = string.Equals(
            uri.Scheme,
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase);
        var isAzureBlobHost = !string.IsNullOrWhiteSpace(uri.Host)
            && uri.Host.EndsWith(
                ".blob.core.windows.net",
                StringComparison.OrdinalIgnoreCase);

        if (!isHttps
            || !isAzureBlobHost
            || !string.IsNullOrWhiteSpace(uri.Query)
            || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new InvalidOperationException(
                "Invalid Azure Storage account URL. Expected an HTTPS Blob service URL " +
                "such as 'https://account.blob.core.windows.net'.");
        }

        // Container and Blob paths are configured separately from the account endpoint.
        return string.IsNullOrEmpty(uri.AbsolutePath.Trim('/'))
            ? uri
            : new UriBuilder(uri) { Path = string.Empty }.Uri;
    }

    private static string GetRequiredConnectionString(
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppeteeDb")
            ?? configuration.GetConnectionString("Default");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            "Missing connection string: AppeteeDb or Default. " +
            "Set 'ConnectionStrings__AppeteeDb' in App Service configuration.");
    }

    private static string GetRequiredConfigurationValue(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Missing configuration: {key}. Set it in appsettings or as environment " +
            $"variable '{key.Replace(":", "__")}'.");
    }
}
