/*
 * Purpose: Verifies that authentication tickets apply the approved persistence and server-owned expiry policy.
 * Created: 2026-08-21T11:14:12-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

using Appetee.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Appetee.Api.Tests.Unit;

public sealed class AuthCookieServiceTests
{
    private static readonly DateTimeOffset IssuedUtc =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignInAsync_AppliesServerOwnedCookieProperties(bool rememberMe)
    {
        var authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        var service = new AuthCookieService(
            new FixedTimeProvider(IssuedUtc),
            new AuthSessionSettings(
                TimeSpan.FromDays(7),
                TimeSpan.FromDays(30)));

        await service.SignInAsync(
            context,
            42,
            "cookie_user",
            rememberMe);

        Assert.NotNull(authentication.Properties);
        Assert.Equal(rememberMe, authentication.Properties!.IsPersistent);
        Assert.True(authentication.Properties.AllowRefresh);
        Assert.Equal(IssuedUtc, authentication.Properties.IssuedUtc);
        Assert.Equal(IssuedUtc.AddDays(7), authentication.Properties.ExpiresUtc);
        Assert.Equal(
            IssuedUtc.ToUnixTimeSeconds().ToString(),
            authentication.Principal?.FindFirstValue(
                AuthSessionPolicy.OriginalIssuedUtcClaim));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? Principal { get; private set; }
        public AuthenticationProperties? Properties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(
            HttpContext context,
            string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            Principal = principal;
            Properties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
