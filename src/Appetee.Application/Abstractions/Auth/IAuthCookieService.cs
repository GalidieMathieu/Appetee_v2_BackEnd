using Appetee.Application.Dtos;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public interface IAuthCookieService
{
    Task SignInAsync(
        HttpContext http,
        int userId,
        string username,
        bool rememberMe = false,
        IEnumerable<Claim>? extraClaims = null);
    Task SignOutAsync(HttpContext http);
    UserSessionDto? GetSession(ClaimsPrincipal user);
}
