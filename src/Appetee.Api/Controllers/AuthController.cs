using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("/api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPasswordRecoveryService _passwordRecoveryService;

        public AuthController(
            IAuthService authService,
            IPasswordRecoveryService passwordRecoveryService)
        {
            _authService = authService;
            _passwordRecoveryService = passwordRecoveryService;
        }

        [HttpPost("sign-up")]
        public async Task<ActionResult<AuthResult>> SignUp([FromBody] SignUpRequest request, CancellationToken ct)
        {
            var authResult = await _authService.SignUpAsync(HttpContext, request, ct);
            return Ok(authResult);
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResult>> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
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

        [AllowAnonymous]
        [HttpPost("password-recovery/request")]
        public async Task<ActionResult<PasswordRecoveryRequestDto>> RequestPasswordRecovery(
            [FromBody] PasswordRecoveryRequest request,
            CancellationToken ct)
        {
            var result = await _passwordRecoveryService.RequestAsync(request, ct);
            return Accepted(result);
        }

        [AllowAnonymous]
        [HttpPost("password-recovery/confirm")]
        public async Task<IActionResult> ConfirmPasswordRecovery(
            [FromBody] PasswordRecoveryConfirmRequest request,
            CancellationToken ct)
        {
            await _passwordRecoveryService.ConfirmAsync(request, ct);
            return NoContent();
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
            var session = _authService.GetRequiredSession(HttpContext);
            return Ok(session);
        }
    }
}
