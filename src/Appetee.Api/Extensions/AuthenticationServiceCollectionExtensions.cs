/*
 * Purpose: Registers and validates authentication, session, and password-recovery services outside Program.cs.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

using Appetee.Api.Configuration;
using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Infrastructure.Auth;
using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace Appetee.Api.Extensions;

/// <summary>Provides the complete dependency-injection boundary for the F-002 authentication feature.</summary>
public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAppeteeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<AppeteeAuthenticationOptions>()
            .Bind(configuration.GetSection(AppeteeAuthenticationOptions.SectionName))
            .Validate(
                IsValidAuthenticationConfiguration,
                "Authentication must define a cookie name, a positive remembered lifetime, " +
                "and an absolute lifetime at least as long as the remembered lifetime.")
            .ValidateOnStart();

        services
            .AddOptions<PasswordRecoveryOptions>()
            .Bind(configuration.GetSection(PasswordRecoveryOptions.SectionName))
            .Validate(
                IsValidRecoveryPolicy,
                "Password recovery token lifetime must be 5–1440 minutes and cooldown must be 0–3600 seconds.")
            .Validate(
                options => IsValidResetPageUrl(options, environment),
                "PasswordRecovery:ResetPageUrl must be an absolute URL without credentials, " +
                "query, or fragment, and must use HTTPS outside development.")
            .ValidateOnStart();

        services
            .AddOptions<CommunicationEmailOptions>()
            .Bind(configuration.GetSection(CommunicationEmailOptions.SectionName))
            .Validate(
                HasValidCommunicationEmailEndpoint,
                "CommunicationEmail:Endpoint must be an absolute HTTPS URL without credentials, query, or fragment.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AccessKey)
                    && options.AccessKey.Length <= 4096,
                "CommunicationEmail:AccessKey must be configured and no more than 4096 characters.")
            .Validate(
                HasValidCommunicationEmailSender,
                "CommunicationEmail:FromAddress must be a valid email address no more than 254 characters.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<AppeteeAuthenticationOptions>>()
                .Value;

            return new AuthSessionSettings(
                TimeSpan.FromDays(options.RememberedSessionDays),
                TimeSpan.FromDays(options.AbsoluteSessionDays));
        });

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<PasswordRecoveryOptions>>()
                .Value;

            return new PasswordRecoverySettings(
                TimeSpan.FromMinutes(options.TokenLifetimeMinutes),
                TimeSpan.FromSeconds(options.RequestCooldownSeconds));
        });

        services.AddSingleton(serviceProvider =>
        {
            var recoveryOptions = serviceProvider
                .GetRequiredService<IOptions<PasswordRecoveryOptions>>()
                .Value;
            var emailOptions = serviceProvider
                .GetRequiredService<IOptions<CommunicationEmailOptions>>()
                .Value;

            return new CommunicationEmailDeliverySettings(
                new Uri(recoveryOptions.ResetPageUrl, UriKind.Absolute),
                emailOptions.FromAddress);
        });

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<CommunicationEmailOptions>>()
                .Value;

            return new EmailClient(
                new Uri(options.Endpoint, UriKind.Absolute),
                new AzureKeyCredential(options.AccessKey));
        });

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();

        services
            .AddOptions<CookieAuthenticationOptions>(
                CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<
                IOptions<AppeteeAuthenticationOptions>,
                TimeProvider>((cookieOptions, configuredOptions, timeProvider) =>
                {
                    ConfigureCookie(
                        cookieOptions,
                        configuredOptions.Value,
                        timeProvider);
                });

        services.AddAuthorization();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthQueries, AuthQueries>();
        services.AddScoped<IPasswordRecoveryRepository, PasswordRecoveryRepository>();
        services.AddSingleton<IPasswordHasher, AspNetIdentityPasswordHasher>();
        services.AddSingleton<IAuthCookieService, AuthCookieService>();
        services.AddSingleton<IPasswordRecoveryTokenProtector, PasswordRecoveryTokenProtector>();
        services.AddSingleton<ICommunicationEmailClient, AzureCommunicationEmailClient>();
        services.AddSingleton<IPasswordRecoveryEmailSender, AzureCommunicationPasswordRecoveryEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();

        return services;
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions cookieOptions,
        AppeteeAuthenticationOptions configuredOptions,
        TimeProvider timeProvider)
    {
        var absoluteLifetime = TimeSpan.FromDays(
            configuredOptions.AbsoluteSessionDays);

        cookieOptions.Cookie.Name = configuredOptions.CookieName;
        cookieOptions.Cookie.Path = "/";
        cookieOptions.Cookie.HttpOnly = true;
        cookieOptions.Cookie.SameSite = SameSiteMode.None;
        cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookieOptions.ExpireTimeSpan = TimeSpan.FromDays(
            configuredOptions.RememberedSessionDays);
        cookieOptions.SlidingExpiration = true;
        cookieOptions.TimeProvider = timeProvider;

        cookieOptions.Events.OnValidatePrincipal = async context =>
        {
            if (!AuthSessionPolicy.HasExceededAbsoluteLifetime(
                    context.Principal,
                    timeProvider.GetUtcNow(),
                    absoluteLifetime))
            {
                return;
            }

            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
        };

        cookieOptions.Events.OnRedirectToLogin = context =>
        {
            var sessionExpired = context.HttpContext.Items.ContainsKey(
                AuthSessionContext.ExpiredSessionItemKey);

            return WriteAuthenticationProblemAsync(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                sessionExpired
                    ? "Your session expired. Please log in again."
                    : "Authentication is required to access this resource.",
                sessionExpired ? "session_expired" : null);
        };

        cookieOptions.Events.OnRedirectToAccessDenied = context =>
            WriteAuthenticationProblemAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You are not authorized to access this resource.");
    }

    private static Task WriteAuthenticationProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? code = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["traceId"] = context.TraceIdentifier,
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            extensions["code"] = code;
        }

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            instance: context.Request.Path,
            extensions: extensions).ExecuteAsync(context);
    }

    private static bool IsValidAuthenticationConfiguration(
        AppeteeAuthenticationOptions options) =>
        !string.IsNullOrWhiteSpace(options.CookieName)
        && options.RememberedSessionDays > 0
        && options.AbsoluteSessionDays >= options.RememberedSessionDays;

    private static bool IsValidRecoveryPolicy(PasswordRecoveryOptions options) =>
        options.TokenLifetimeMinutes is >= 5 and <= 1440
        && options.RequestCooldownSeconds is >= 0 and <= 3600;

    private static bool IsValidResetPageUrl(
        PasswordRecoveryOptions options,
        IHostEnvironment environment)
    {
        if (!Uri.TryCreate(options.ResetPageUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var hasAllowedScheme = uri.Scheme == Uri.UriSchemeHttps
            || (environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp);

        return options.ResetPageUrl.Length <= 2048
            && hasAllowedScheme
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool HasValidCommunicationEmailEndpoint(
        CommunicationEmailOptions options) =>
        !string.IsNullOrWhiteSpace(options.Endpoint)
        && options.Endpoint.Length <= 2048
        && Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);

    private static bool HasValidCommunicationEmailSender(
        CommunicationEmailOptions options) =>
        !string.IsNullOrWhiteSpace(options.FromAddress)
        && options.FromAddress.Length <= 254
        && MailAddress.TryCreate(options.FromAddress, out var address)
        && string.Equals(
            address.Address,
            options.FromAddress,
            StringComparison.OrdinalIgnoreCase);
}
