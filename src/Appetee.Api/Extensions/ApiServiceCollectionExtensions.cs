/*
 * Purpose: Registers controllers, OpenAPI, CORS, and other HTTP-boundary services outside Program.cs.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T01:18:52-06:00
 */

using Microsoft.OpenApi.Models;
using Appetee.Api.Security;
using Appetee.Api.Helpers;
using System.Reflection;

namespace Appetee.Api.Extensions;

/// <summary>Provides API presentation-layer registrations and production-safe CORS configuration.</summary>
internal static class ApiServiceCollectionExtensions
{
    internal const string FrontendCorsPolicy = "AngularFront";

    public static IServiceCollection AddAppeteeApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? [];

        if (!environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Missing production CORS configuration. Set Cors:AllowedOrigins " +
                "or environment variables such as 'Cors__AllowedOrigins__0'.");
        }

        services.AddControllers();
        services.AddSingleton(new BrowserRequestOriginPolicy(allowedOrigins));
        services.AddAntiforgery(options =>
        {
            options.HeaderName = BrowserCsrfMiddleware.HeaderName;
            options.Cookie.Name = BrowserCsrfMiddleware.CookieName;
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.IsEssential = true;
            options.SuppressXFrameOptionsHeader = true; // Existing middleware supplies DENY.
        });
        services.AddSingleton(TimeProvider.System);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => ConfigureOpenApi(options));
        services.AddCors(options =>
        {
            options.AddPolicy(
                FrontendCorsPolicy,
                policy => ConfigureFrontendCors(
                    policy,
                    allowedOrigins,
                    environment));
        });

        return services;
    }

    private static void ConfigureOpenApi(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        options.OperationFilter<CsrfOperationFilter>();
        options.OperationFilter<CatalogueOperationFilter>();
        options.OperationFilter<MealPlannerOperationFilter>();
        options.SchemaFilter<MealPlannerSchemaFilter>();
        options.SchemaFilter<NutritionProfileSchemaFilter>();
        options.SchemaFilter<NutritionCalculationSchemaFilter>();
        options.SchemaFilter<NutritionPlanSchemaFilter>();
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "Appetee API",
                Version = "v1",
            });

        var xmlFile = $"{Assembly.GetEntryAssembly()?.GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }

    private static void ConfigureFrontendCors(
        Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder policy,
        string[] allowedOrigins,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            // Without an explicit frontend allowlist, permit same-origin access only.
            policy
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .WithExposedHeaders("ETag")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }
}
