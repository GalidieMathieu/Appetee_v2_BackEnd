using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Ingredients;
using Appetee.Application.Services.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Appetee.Application.Models.Catalogue;
using Appetee.Api.Helpers;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    [CatalogueBoundary]
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
        [Authorize(Policy = CataloguePermissions.ReadAdmin)]
        public async Task<ActionResult<CatalogueResourceDto<IngredientAdminDetailDto>>> GetIngredientDetailsById(int id, CancellationToken ct)
        {
            var ingredient = await _ingredientS.GetIngredientWithDetailsByIdAsync(id, ct);

            if (ingredient is null)
                return NotFound();

            return Ok(CatalogueEtags.Result(Response,"ingredient",id,ingredient,ingredient.CatalogueMetadata!));
        }

        [HttpGet("recipe-details/{id:int}")]
        [Authorize(Policy = CataloguePermissions.ReadAdmin)]
        public async Task<ActionResult<CatalogueResourceDto<RecipeDetailDto>>> GetRecipeDetailsById(int id, CancellationToken ct)
        {
            var recipe = await _recipeS.GetRecipeWithDetailsByIdAsync(id,ct);
            return recipe is null ? NotFound() : Ok(CatalogueEtags.Result(Response,"recipe",id,recipe,recipe.CatalogueMetadata!));
        }

        [HttpPost("ingredient-details")]
        [Authorize(Policy = CataloguePermissions.Write)]
        public async Task<ActionResult<CatalogueResourceDto<IngredientAdminDetailDto>>> CreateIngredientWithDetailsAsync([FromForm] IngredientAdminDetailRequest request, CancellationToken ct)
        {
            var ingredientCreated = await _ingredientS.CreateIngredientWithDetailsAsync(request, ct);
            if (ingredientCreated is null)
                return NotFound();

            return Ok(CatalogueEtags.Result(Response,"ingredient",ingredientCreated.Id,ingredientCreated,ingredientCreated.CatalogueMetadata!));
        }

        [HttpPost("recipe-details")]
        [Authorize(Policy = CataloguePermissions.Write)]
        public async Task<ActionResult<CatalogueResourceDto<RecipeSummaryDto>>> CreateRecipeWithDetailsAsync([FromForm] RecipeDetailRequest request, CancellationToken ct)
        {
            var recipeCreated = await _recipeS.CreateRecipeWithDetailsAsync(request, ct);
            if (recipeCreated is null)
                return NotFound();

            return Ok(CatalogueEtags.Result(Response,"recipe",recipeCreated.Id,recipeCreated,recipeCreated.CatalogueMetadata!));
        }

        [HttpPut("recipe-details/{id:int}")]
        [Authorize(Policy = CataloguePermissions.Write)]
        [CataloguePrecondition("recipe")]
        public async Task<ActionResult<CatalogueResourceDto<RecipeSummaryDto>>> UpdateRecipeWithDetailsAsync(int id, [FromForm] RecipeDetailRequest request, CancellationToken ct)
        {
            var recipeUpdated = await _recipeS.UpdateRecipeWithDetailsAsync(id, request, ct);
            if (recipeUpdated is null)
                return NotFound();

            return Ok(CatalogueEtags.Result(Response,"recipe",id,recipeUpdated,recipeUpdated.CatalogueMetadata!));
        }
    }
}
