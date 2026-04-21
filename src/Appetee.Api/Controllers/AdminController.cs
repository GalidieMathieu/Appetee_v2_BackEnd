using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Ingredients;
using Appetee.Application.Services.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize/*(Roles = "Admin")*/] //as admin, only admin account will be able to use this controller. 
    public sealed class AdminController : ControllerBase
    {
        private readonly IIngredientService _ingredientS;
        private readonly IRecipeService _recipeS;

        public AdminController(IIngredientService ingredientS, IRecipeService recipeS)
        {
            _ingredientS = ingredientS;
            _recipeS = recipeS;
        }


        [HttpGet("ingredient-details/{id:int}")]
        public async Task<ActionResult<IngredientAdminDetailDto>> GetIngredientDetailsById(int id, CancellationToken ct)
        {
            var ingredient = await _ingredientS.GetIngredientWithDetailsByIdAsync(id, ct);

            if (ingredient is null)
                return NotFound();

            return Ok(ingredient);
        }

        [HttpPost("ingredient-details")]
        public async Task<ActionResult<IngredientAdminDetailDto>> CreateIngredientWithDetailsAsync([FromForm] IngredientAdminDetailRequest request, CancellationToken ct)
        {
            var ingredientCreated = await _ingredientS.CreateIngredientWithDetailsAsync(request, ct);
            if (ingredientCreated is null)
                return NotFound();

            return Ok(ingredientCreated);
        }

        [HttpPost("recipe-details")]
        public async Task<ActionResult<RecipeSummaryDto>> CreateRecipeWithDetailsAsync([FromForm] RecipeDetailRequest request, CancellationToken ct)
        {
            var recipeCreated = await _recipeS.CreateRecipeWithDetailsAsync(request, ct);
            if (recipeCreated is null)
                return NotFound();

            return Ok(recipeCreated);
        }

        [HttpPut("recipe-details/{id:int}")]
        public async Task<ActionResult<RecipeSummaryDto>> UpdateRecipeWithDetailsAsync(int id, [FromForm] RecipeDetailRequest request, CancellationToken ct)
        {
            var recipeUpdated = await _recipeS.UpdateRecipeWithDetailsAsync(id, request, ct);
            if (recipeUpdated is null)
                return NotFound();

            return Ok(recipeUpdated);
        }
    }
}
