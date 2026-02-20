using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("id must be > 0");

        var user = await _users.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("exists-by-email")]
    public async Task<ActionResult> CheckUserExist([FromQuery] string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) return BadRequest("Email must be valid.");


        var exists = await _users.ExistsByEmailAsync(email, ct);  
        return Ok(new { exists }); ;
    }

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
    }
}