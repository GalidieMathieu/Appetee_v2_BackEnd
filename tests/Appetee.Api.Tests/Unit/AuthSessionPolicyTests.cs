/*
 * Purpose: Verifies the absolute session boundary and fail-closed handling of invalid issuance claims.
 * Created: 2026-08-21T11:06:26-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

using Appetee.Infrastructure.Auth;
using System.Security.Claims;

namespace Appetee.Api.Tests.Unit;

public sealed class AuthSessionPolicyTests
{
    private static readonly DateTimeOffset IssuedUtc =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HasExceededAbsoluteLifetime_AllowsActivityBeforeThirtyDays()
    {
        var principal = CreatePrincipal(IssuedUtc);

        var expired = AuthSessionPolicy.HasExceededAbsoluteLifetime(
            principal,
            IssuedUtc.AddDays(29).AddHours(23),
            TimeSpan.FromDays(30));

        Assert.False(expired);
    }

    [Fact]
    public void HasExceededAbsoluteLifetime_RequiresReauthenticationAtThirtyDays()
    {
        var principal = CreatePrincipal(IssuedUtc);

        var expired = AuthSessionPolicy.HasExceededAbsoluteLifetime(
            principal,
            IssuedUtc.AddDays(30),
            TimeSpan.FromDays(30));

        Assert.True(expired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-timestamp")]
    public void HasExceededAbsoluteLifetime_RejectsMissingOrInvalidIssueTime(string? value)
    {
        var claims = value is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(AuthSessionPolicy.OriginalIssuedUtcClaim, value) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));

        Assert.True(AuthSessionPolicy.HasExceededAbsoluteLifetime(
            principal,
            IssuedUtc,
            TimeSpan.FromDays(30)));
    }

    private static ClaimsPrincipal CreatePrincipal(DateTimeOffset issuedUtc) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(
                    AuthSessionPolicy.OriginalIssuedUtcClaim,
                    issuedUtc.ToUnixTimeSeconds().ToString())
            },
            "cookie"));
}
