using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.Users;
using Appetee.Application.utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private const string LegacyAccountRouteDetail =
        "This account route is not available.";

    private readonly IUserService _users;
    private readonly IAuthService _authService;

    public UsersController(
        IUserService users,
        IAuthService authService)
    {
        _users = users;
        _authService = authService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserProfileDto>> GetMe(
        CancellationToken ct)
    {
        var currentUserId = _authService.GetRequiredUserId(HttpContext);
        var profile = await _users.GetCurrentProfileAsync(currentUserId, ct);

        if (profile is null)
        {
            throw new UnauthorizedException(
                "Session is no longer valid.");
        }

        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<CurrentUserProfileDto>> UpdateMe(
        [FromBody] UpdateCurrentUserProfileRequest request,
        CancellationToken ct)
    {
        var currentUserId = _authService.GetRequiredUserId(HttpContext);
        var profile = await _users.UpdateCurrentProfileAsync(
            currentUserId,
            request,
            ct);

        if (profile is null)
        {
            throw new UnauthorizedException(
                "Session is no longer valid.");
        }

        return Ok(profile);
    }

    // Phase 1 containment: keep explicit tombstones for the old consumer
    // routes so authenticated callers receive the same non-leaking response
    // for their own ID, another user's ID, and an ID that does not exist.
    // These endpoints are hidden from OpenAPI and perform no database work.

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id) => LegacyAccountRouteNotFound();

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public IActionResult List() => LegacyAccountRouteNotFound();

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("exists-by-email")]
    public IActionResult CheckUserExist() => LegacyAccountRouteNotFound();

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPut("{id:int}")]
    public IActionResult Update(int id) => LegacyAccountRouteNotFound();

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => LegacyAccountRouteNotFound();

    private ObjectResult LegacyAccountRouteNotFound() =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: LegacyAccountRouteDetail);

}
