using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Microsoft.AspNetCore.Http;

namespace Appetee.Application.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResult> SignUpAsync(HttpContext http ,SignUpRequest request, CancellationToken ct);
        //Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);
        //Task LogoutAsync(string? sessionToken, CancellationToken ct);
        //Task<Appetee.Application.Dtos.UserDto?> GetMeAsync(string? sessionToken, CancellationToken ct);
    }
}
