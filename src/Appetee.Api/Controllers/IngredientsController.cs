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

        // Any authenticated user can read Ingredients
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IReadOnlyList<IngredientDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<IngredientDto>>> GetAll(CancellationToken ct)
        {
            var ingredients = await _ingredientS.GetAll(ct);
            return Ok(ingredients);
        }
    }
}
