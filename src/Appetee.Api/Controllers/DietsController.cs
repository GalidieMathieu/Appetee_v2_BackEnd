using Appetee.Application.Dtos;
using Appetee.Application.Services.Diets;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers;

[ApiController]
[Route("api/diets")]
public sealed class DietsController : ControllerBase
{
    private readonly IDietService _diets;

    public DietsController(IDietService diets) => _diets = diets;

    // Any authenticated user can read diets
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DietDto>>> GetAll(CancellationToken ct) {
        var diet = await _diets.GetAll(ct);
        return Ok(diet);
    }

}