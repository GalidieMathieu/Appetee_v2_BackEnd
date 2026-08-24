/*
 * Purpose: Enforces the absolute authentication lifetime that sliding cookie renewal must not extend.
 * Created: 2026-08-21T11:06:14-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

using System.Globalization;
using System.Security.Claims;

namespace Appetee.Infrastructure.Auth;

public static class AuthSessionPolicy
{
    // This claim remains unchanged when ASP.NET renews the ticket's idle expiry.
    public const string OriginalIssuedUtcClaim = "appetee:original_issued_utc";

    public static bool HasExceededAbsoluteLifetime(
        ClaimsPrincipal? principal,
        DateTimeOffset now,
        TimeSpan absoluteLifetime)
    {
        var issuedValue = principal?.FindFirstValue(OriginalIssuedUtcClaim);

        if (!long.TryParse(
                issuedValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var issuedUnixSeconds))
        {
            return true;
        }

        DateTimeOffset issuedUtc;

        try
        {
            issuedUtc = DateTimeOffset.FromUnixTimeSeconds(issuedUnixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }

        return issuedUtc > now || now - issuedUtc >= absoluteLifetime;
    }
}

/// Holds the configured idle and absolute lifetimes used by cookie issuance and validation.
public sealed record AuthSessionSettings(
    TimeSpan RememberedSessionLifetime,
    TimeSpan AbsoluteSessionLifetime);
