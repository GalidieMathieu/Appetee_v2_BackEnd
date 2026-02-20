using Appetee.Application.Dtos;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public interface IAuthCookieService
{
    Task SignInAsync(HttpContext http, int userId, string username, IEnumerable<Claim>? extraClaims = null /*DateTimeOffset? expiresUtc = null*/);
    Task SignOutAsync(HttpContext http);
    UserSessionDto? GetSession(ClaimsPrincipal user);
}