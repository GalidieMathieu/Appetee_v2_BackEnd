using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [Route("api/recipes")]
    [Authorize]
    public sealed class RecipesController : ControllerBase
    {
        private readonly IRecipeService _recipes;
        private readonly IAuthService _authService;

        public RecipesController(
            IRecipeService recipes,
            IAuthService authService)
        {
            _recipes = recipes;
            _authService = authService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(RecipeDiscoveryPageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<RecipeDiscoveryPageDto>> Discover(
            CancellationToken ct,
            [FromQuery(Name = "cursor")] string? cursor = null,
            [FromQuery(Name = "limit")] int limit = 20)
        {
            var currentUserId = _authService.GetRequiredUserId(HttpContext);
            var request = new RecipeDiscoveryRequest
            {
                Cursor = cursor,
                Limit = limit,
            };
            var page = await _recipes.DiscoverAsync(currentUserId, request, ct);
            return Ok(page);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<RecipeDetailDto>> GetRecipeWithDetails(int id, CancellationToken ct)
        {
            var recipe = await _recipes.GetRecipeWithDetailsByIdAsync(id, ct);

            if (recipe is null)
                return NotFound();

            return Ok(recipe);
        }

        [HttpGet("meal-preps")]
        public ActionResult GetMealPreps()
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Meal Prep backend is not implemented yet.",
                detail: "The frontend currently uses a local placeholder flow while the Meal Prep backend contract is still evolving."
            );
        }

        [HttpGet("meal-preps/{id:int}")]
        public ActionResult GetMealPrepById(int id)
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Meal Prep backend is not implemented yet.",
                detail: $"Meal Prep {id} cannot be loaded from the backend yet. The frontend is still using temporary local persistence."
            );
        }

        [HttpPost("meal-preps")]
        public ActionResult CreateMealPrep([FromBody] MealPrepPlanRequest request)
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Meal Prep backend is not implemented yet.",
                detail: "Meal Prep create persistence has been reserved, but the backend business logic is intentionally still pending."
            );
        }

        [HttpPut("meal-preps/{id:int}")]
        public ActionResult UpdateMealPrep(int id, [FromBody] MealPrepPlanRequest request)
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Meal Prep backend is not implemented yet.",
                detail: $"Meal Prep {id} cannot be updated on the backend yet because the contract is still being finalized."
            );
        }

        [HttpDelete("meal-preps/{id:int}")]
        public ActionResult DeleteMealPrep(int id)
        {
            return Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Meal Prep backend is not implemented yet.",
                detail: $"Meal Prep {id} cannot be deleted on the backend yet because persistence is still a placeholder."
            );
        }

    }

    public sealed record MealPrepPlanRequest(
        MealPrepProfileRequest Profile,
        MealPrepTargetRequest Target,
        MealPrepDiscoveryRequest Discovery,
        IReadOnlyList<int> SelectedRecipeIds,
        IReadOnlyList<MealPrepSlotRequest> Slots
    );

    public sealed record MealPrepProfileRequest(
        IReadOnlyList<int>? DietIds,
        IReadOnlyList<int>? IngredientRestrictionIds
    );

    public sealed record MealPrepTargetRequest(
        string Name,
        int DurationWeeks,
        int MealsPerDay,
        bool TrackCalories,
        decimal? CalorieLimit,
        bool TrackProtein,
        decimal? ProteinTarget,
        bool TrackBudget,
        decimal? BudgetLimit,
        int? MaxPrepTimeMinutes,
        string? Difficulty,
        bool FreezerFriendlyOnly
    );

    public sealed record MealPrepDiscoveryRequest(
        string Mode,
        string RecipeQuery,
        string IngredientQuery,
        string SortBy
    );

    public sealed record MealPrepSlotRequest(
        string Id,
        int WeekIndex,
        int DayIndex,
        string DayLabel,
        int MealIndex,
        string MealLabel,
        int? RecipeId
    );
}
