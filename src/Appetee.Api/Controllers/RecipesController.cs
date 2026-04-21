using Appetee.Application.Dtos;
using Appetee.Application.Services.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("api/recipes")]
    [Authorize]
    public sealed class RecipesController : ControllerBase
    {
        private readonly IRecipeService _recipes;

        public RecipesController(IRecipeService recipes) => _recipes = recipes;

        [HttpGet("{id:int}")]
        public async Task<ActionResult<RecipeDetailDto>> GetRecipeWithDetails(int id, CancellationToken ct)
        {
            var recipe = await _recipes.GetRecipeWithDetailsByIdAsync(id, ct);

            if (recipe is null)
                return NotFound();

            return Ok(recipe);
        }
    }
}
