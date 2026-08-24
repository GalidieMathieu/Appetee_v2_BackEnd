/*
 * Purpose: Defines the ordered HTTP middleware pipeline used by the Appetee API.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T01:18:52-06:00
 */

using Appetee.Api.Configuration;
using Appetee.Application.Abstractions.Auth;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Appetee.Api.Extensions;

/// <summary>Composes request diagnostics, security middleware, authentication, and API endpoints.</summary>
internal static class WebApplicationExtensions
{
    public static WebApplication UseAppeteePipeline(this WebApplication app)
    {
        app.UseRequestLoggingContext();
        app.UseExceptionHandler("/error");

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseSecurityHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint(
                    "/swagger/v1/swagger.json",
                    "Appetee API v1");
            });
        }

        app.UseRouting();
        app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicy);
        app.UseAuthentication();
        app.UseExpiredSessionMarker();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }

    private static void UseRequestLoggingContext(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var traceId = Activity.Current?.TraceId.ToString();
            if (string.IsNullOrWhiteSpace(traceId))
            {
                traceId = context.TraceIdentifier;
            }

            using var scope = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Appetee.Request")
                .BeginScope(new Dictionary<string, object?>
                {
                    ["TraceId"] = traceId,
                    ["RequestPath"] = context.Request.Path.Value ?? string.Empty,
                    ["HttpMethod"] = context.Request.Method,
                });

            await next();
        });
    }

    private static void UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            await next();
        });
    }

    private static void UseExpiredSessionMarker(this IApplicationBuilder app)
    {
        var cookieName = app.ApplicationServices
            .GetRequiredService<IOptions<AppeteeAuthenticationOptions>>()
            .Value
            .CookieName;

        app.Use(async (context, next) =>
        {
            // A rejected cookie and an anonymous request need different user-facing outcomes.
            if (context.User.Identity?.IsAuthenticated != true
                && context.Request.Cookies.ContainsKey(cookieName))
            {
                context.Items[AuthSessionContext.ExpiredSessionItemKey] = true;
            }

            await next();
        });
    }
}
