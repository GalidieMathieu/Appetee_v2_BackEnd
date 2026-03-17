using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Ingredients;
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

        public AdminController(IIngredientService ingredientS) => _ingredientS = ingredientS;


        [HttpGet("ingredient-details/{id:int}")]
        public async Task<ActionResult<IngredientAdminDetailDto>> GetIngredientDetailsById(int id, CancellationToken ct)
        {
            var ingredient = await _ingredientS.GetIngredientWithDetailsByIdAsync(id, ct);

            if (ingredient is null)
                return NotFound();

            return Ok(ingredient);
        }

        [HttpPost("ingredient-details")]
        public async Task<ActionResult<IngredientAdminDetailDto>> CreateIngredientWithDetailsAsync([FromBody] IngredientAdminDetailRequest request, CancellationToken ct)
        {
            var ingredientCreated = await _ingredientS.CreateIngredientWithDetailsAsync(request, ct);
            if (ingredientCreated is null)
                return NotFound();

            return Ok(ingredientCreated);
        }
    }
}