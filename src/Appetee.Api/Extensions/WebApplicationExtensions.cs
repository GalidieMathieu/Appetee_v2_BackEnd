/*
 * Purpose: Defines the ordered HTTP middleware pipeline used by the Appetee API.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T01:18:52-06:00
 */

using Appetee.Api.Configuration;
using Appetee.Api.Security;
using Appetee.Application.Abstractions.Auth;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Appetee.Api.Diagnostics;

namespace Appetee.Api.Extensions;

/// <summary>Composes request diagnostics, security middleware, authentication, and API endpoints.</summary>
internal static class WebApplicationExtensions
{
    public static WebApplication UseAppeteePipeline(this WebApplication app)
    {
        app.UseMiddleware<RequestDiagnosticsMiddleware>();
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
        app.Use((context, next) =>
        {
            if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>() is null
                && context.Items[RequestDiagnosticsMiddleware.ContextKey] is RequestLogContext request)
                request.CaptureEndpoint(context);
            return next(context);
        });
        app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicy);
        app.UseAuthentication();
        app.UseExpiredSessionMarker();
        app.UseAuthorization();
        app.UseMiddleware<BrowserCsrfMiddleware>();
        app.MapControllers();

        var lifecycleLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Appetee.Startup");
        lifecycleLogger.LogInformation(new EventId(1100), "Application starting.");
        app.Lifetime.ApplicationStarted.Register(() => lifecycleLogger.LogInformation(new EventId(1101), "Application started."));
        app.Lifetime.ApplicationStopping.Register(() => lifecycleLogger.LogInformation(new EventId(1102), "Application stopping."));

        return app;
    }

    private static void UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            // Apply before authentication/authorization, including their 401/403 responses.
            if (context.Request.Path.StartsWithSegments("/api/auth")
                || context.Request.Path.StartsWithSegments("/api/users")
                || context.Request.Path.StartsWithSegments("/api/admin")
                || context.Request.Path.StartsWithSegments("/api/nutrition-plans")
                || context.Request.Path.StartsWithSegments("/api/meal-plan"))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Pragma = "no-cache";
            }
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
