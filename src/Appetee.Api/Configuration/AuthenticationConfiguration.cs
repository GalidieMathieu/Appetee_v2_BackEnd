/*
 * Purpose: Defines typed API configuration for cookie authentication and password recovery startup validation.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

namespace Appetee.Api.Configuration;

/// <summary>Maps server-owned cookie names and session lifetimes from configuration.</summary>
internal sealed class AppeteeAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string CookieName { get; init; } = "__Host-appetee";

    public int RememberedSessionDays { get; init; } = 7;

    public int AbsoluteSessionDays { get; init; } = 30;
}

/// <summary>Maps recovery policy and the environment-specific frontend reset destination.</summary>
internal sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";

    public int TokenLifetimeMinutes { get; init; } = 60;

    public int RequestCooldownSeconds { get; init; } = 60;

    public string ResetPageUrl { get; init; } = string.Empty;
}

/// <summary>Maps the Azure Communication Services Email endpoint, access key, and sender identity.</summary>
internal sealed class CommunicationEmailOptions
{
    public const string SectionName = "CommunicationEmail";

    public string Endpoint { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;
}
