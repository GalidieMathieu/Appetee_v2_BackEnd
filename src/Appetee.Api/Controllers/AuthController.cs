using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Application.utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("/api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService) => _authService = authService;

        [HttpPost("sign-up")]
        public async Task<ActionResult<AuthResult>> SignUp([FromBody] SignUpRequest request, CancellationToken ct)
        {
            Console.WriteLine(request);
            var authResult = await _authService.SignUpAsync(HttpContext, request, ct);
            return Ok(authResult);
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResult>> login([FromBody] LoginRequest request, CancellationToken ct)
        {
            Console.WriteLine(request);
            var authResult = await _authService.LogInAsync(HttpContext, request, ct);
            return Ok(authResult);
        }

        [AllowAnonymous]
        [HttpGet("exists-by-email")]
        public async Task<ActionResult<EmailExistsDto>> CheckUserExist(
            [FromQuery] string email,
            CancellationToken ct)
        {
            var result = await _authService.ExistsByEmailAsync(email, ct);
            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> logout(CancellationToken ct)
        {
            await _authService.LogOutAsync(HttpContext, ct);
            return NoContent();
        }

        [HttpGet("session")]
        public async Task<ActionResult<UserSessionDto>> session(CancellationToken ct)
        {
            var session = _authService.GetSession(HttpContext);
            if (session is null)
            {
                throw new UnauthorizedException("Missing or invalid authentication cookie.");
            }
            int userId = session.userId;
            if (userId <= 0)
            {
                throw new UnauthorizedException("Missing or invalid authentication cookie.");
            }
            return Ok(session);
        }
    }
}
