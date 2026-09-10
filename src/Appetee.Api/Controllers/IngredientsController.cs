// Purpose: Exposes the public ingredient catalogue and bounded autocomplete HTTP contract.
// Change reason: Preserve the existing GetAll catalogue path while adding F-008 Phase 9 autocomplete routing.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-27T13:46:36-06:00

using Appetee.Application.Dtos;
using Appetee.Application.Services.Ingredients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("api/ingredients")]
    public sealed class IngredientsController : ControllerBase
    {
        private readonly IIngredientService _ingredientS;

        public IngredientsController(IIngredientService ingredientS) => _ingredientS = ingredientS;

        /// <summary>Preserves the full catalogue when search is absent and delegates bounded autocomplete when present.</summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IReadOnlyList<IngredientDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IReadOnlyList<IngredientDto>>> GetAll(
            CancellationToken ct,
            [FromQuery(Name = "search")] string? search = null,
            [FromQuery(Name = "limit")] int? limit = null)
        {
            IReadOnlyList<IngredientDto> ingredients;
            if (!Request.Query.ContainsKey("search") && !limit.HasValue)
            {
                ingredients = await _ingredientS.GetAll(ct);
            }
            else
            {
                ingredients = await _ingredientS.SearchAsync(
                    search ?? string.Empty,
                    limit,
                    ct);
            }

            return Ok(ingredients);
        }
    }
}
