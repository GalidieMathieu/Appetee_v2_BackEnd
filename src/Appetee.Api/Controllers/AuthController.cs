using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Api.Models;
using Appetee.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Application.Abstractions.Catalogue;
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
        private readonly IPasswordRecoveryService _passwordRecoveryService;

        public AuthController(
            IAuthService authService,
            IPasswordRecoveryService passwordRecoveryService)
        {
            _authService = authService;
            _passwordRecoveryService = passwordRecoveryService;
        }

        [AllowAnonymous]
        [HttpGet("csrf")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public ActionResult<CsrfTokenDto> Csrf([FromServices] IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new CsrfTokenDto(tokens.RequestToken!, BrowserCsrfMiddleware.HeaderName));
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
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(typeof(UserSessionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserSessionDto>> session(
            [FromServices] ICatalogueAuthorizationQueries authorization,
            [FromServices] ICatalogueActorContext actor,
            CancellationToken ct)
        {
            var session = _authService.GetRequiredSession(HttpContext);
            if (Request.Query.Count != 0)
                throw new ValidationException("The current-user session endpoint accepts no query parameters.");
            var permissions = await authorization.GetPermissionsAsync(session.userId, actor.SecurityVersion, ct);
            if (permissions is null) throw new SessionExpiredException();
            return Ok(session with { Permissions = permissions });
        }
    }
}
