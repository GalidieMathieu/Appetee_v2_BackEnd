using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Appetee.Infrastructure.Auth
{
    public sealed class AuthCookieService : IAuthCookieService
    {
        private readonly TimeProvider _timeProvider;
        private readonly AuthSessionSettings _sessionSettings;

        public AuthCookieService(
            TimeProvider timeProvider,
            AuthSessionSettings sessionSettings)
        {
            _timeProvider = timeProvider;
            _sessionSettings = sessionSettings;
        }

        public async Task SignInAsync(
            HttpContext http,
            int userId,
            string username,
            bool rememberMe = false,
            IEnumerable<Claim>? extraClaims = null)
        {
            var issuedUtc = _timeProvider.GetUtcNow();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, username),
                new(
                    AuthSessionPolicy.OriginalIssuedUtcClaim,
                    issuedUtc.ToUnixTimeSeconds().ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
            };

            if (extraClaims is not null) claims.AddRange(extraClaims);

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    // ExpiresUtc limits the server ticket for every login; IsPersistent alone
                    // controls whether the browser retains the cookie after it closes.
                    IsPersistent = rememberMe,
                    IssuedUtc = issuedUtc,
                    ExpiresUtc = issuedUtc.Add(
                        _sessionSettings.RememberedSessionLifetime)
                });
        }

        public Task SignOutAsync(HttpContext http) =>
            http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        public UserSessionDto? GetSession(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true) return null;

            var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = user.Identity?.Name;

            if (!int.TryParse(idStr, out var id) || string.IsNullOrWhiteSpace(name))
                return null;

            return new UserSessionDto(id, name);
        }
    }
}
