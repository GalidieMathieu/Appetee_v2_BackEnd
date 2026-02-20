using Appetee.Application.Dtos;
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
        public async Task SignInAsync(
        HttpContext http,
        int userId,
        string username,
        IEnumerable<Claim>? extraClaims = null
        /*DateTimeOffset? expiresUtc = null*/) //for later when user have a "stay log in" button
        {
            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
        };

            if (extraClaims is not null) claims.AddRange(extraClaims);

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
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
