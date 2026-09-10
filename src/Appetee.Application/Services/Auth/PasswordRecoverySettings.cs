/*
 * Purpose: Holds server-owned lifetime, throttling, and password boundaries for password recovery.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T01:28:02-06:00
 */

namespace Appetee.Application.Services.Auth;

/// <summary>Configures operational recovery-token lifetime and request cooldown policy.</summary>
public sealed record PasswordRecoverySettings(
    TimeSpan TokenLifetime,
    TimeSpan RequestCooldown)
{
    public const int MinimumPasswordLength = 8;

    public const int MaximumPasswordLength = 128;
}
