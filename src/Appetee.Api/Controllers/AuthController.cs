using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Application.utils;
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
        /*
        // GET /api/users?skip=0&take=20
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> List([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(skip, 0);

            var users = await _users.ListAsync(skip, take, ct);
            return Ok(users);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
        {
            if (id <= 0) return BadRequest("id must be > 0");
            if (request is null) return BadRequest("body is required");

            var updated = await _users.UpdateAsync(id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            if (id <= 0) return BadRequest("id must be > 0");

            var deleted = await _users.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }*/
    }
}
