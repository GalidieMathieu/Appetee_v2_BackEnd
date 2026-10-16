using Appetee.Api.Helpers;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.MealPlans;
using Appetee.Application.Requests.MealPlans;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Api.Controllers;

[ApiController,Authorize,MealPlannerBoundary]
[Route("api/meal-plan")]
public sealed class MealPlannerController(IMealPlannerService planner,IMealService meals,IMealItemService items,IPlannerCopyService copy,IAuthService auth) : ControllerBase
{
    [HttpGet("days")]
    public async Task<ActionResult<MealPlannerRangeDto>> GetRange([FromQuery] string? from,[FromQuery] string? through,CancellationToken ct) =>
        Ok(await planner.GetRangeAsync(auth.GetRequiredUserId(HttpContext),from,through,ct));

    [HttpPost("days/{date}/meals"),Consumes("application/json")]
    [ProducesResponseType(typeof(PlannerMutationDto),StatusCodes.Status201Created)]
    public async Task<ActionResult<PlannerMutationDto>> CreateMeal(string date,[FromBody] CreateMealRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct)
    {
        var result=await meals.CreateAsync(auth.GetRequiredUserId(HttpContext),date,request,MealPlanRevisionHeader.Parse(Request.Headers),ct);
        var calendarDate=result.Days[0].Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        return Created($"/api/meal-plan/days?from={calendarDate}&through={calendarDate}",result);
    }

    [HttpPatch("meals/{mealId:int}"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> RenameMeal(int mealId,[FromBody] RenameMealRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        Ok(await meals.RenameAsync(auth.GetRequiredUserId(HttpContext),mealId,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpDelete("meals/{mealId:int}")]
    public async Task<ActionResult<PlannerMutationDto>> DeleteMeal(int mealId,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        Ok(await meals.DeleteAsync(auth.GetRequiredUserId(HttpContext),mealId,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPost("days/{date}/recipe-items"),Consumes("application/json")]
    [ProducesResponseType(typeof(PlannerMutationDto),StatusCodes.Status201Created)]
    public async Task<ActionResult<PlannerMutationDto>> AddRecipe(string date,[FromBody] AddRecipeItemRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        CreatedDay(await items.AddRecipeAsync(auth.GetRequiredUserId(HttpContext),date,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPut("recipe-items/{itemId:int}/quantity"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> SetRecipeQuantity(int itemId,[FromBody] SetRecipeQuantityRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        Ok(await items.SetRecipeQuantityAsync(auth.GetRequiredUserId(HttpContext),itemId,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPost("days/{date}/manual-items"),Consumes("application/json")]
    [ProducesResponseType(typeof(PlannerMutationDto),StatusCodes.Status201Created)]
    public async Task<ActionResult<PlannerMutationDto>> AddManual(string date,[FromBody] AddManualItemRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        CreatedDay(await items.AddManualAsync(auth.GetRequiredUserId(HttpContext),date,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPut("manual-items/{itemId:int}"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> UpdateManual(int itemId,[FromBody] ManualItemRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        Ok(await items.UpdateManualAsync(auth.GetRequiredUserId(HttpContext),itemId,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpDelete("items/{itemId:int}")]
    public async Task<ActionResult<PlannerMutationDto>> RemoveItem(int itemId,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct) =>
        Ok(await items.RemoveAsync(auth.GetRequiredUserId(HttpContext),itemId,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    private ActionResult<PlannerMutationDto> CreatedDay(PlannerMutationDto result)
    {
        var date=result.Days[0].Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        return Created($"/api/meal-plan/days?from={date}&through={date}",result);
    }

    [HttpPut("days/{date}/meal-order"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> ReorderMeals(string date,[FromBody] ReorderRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct)=>
        Ok(await meals.ReorderAsync(auth.GetRequiredUserId(HttpContext),date,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPut("meals/{mealId:int}/item-order"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> ReorderItems(int mealId,[FromBody] ReorderRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct)=>
        Ok(await items.ReorderAsync(auth.GetRequiredUserId(HttpContext),mealId,request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPost("copy-day"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> CopyDay([FromBody] CopyDayRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct)=>
        Ok(await copy.CopyDayAsync(auth.GetRequiredUserId(HttpContext),request,MealPlanRevisionHeader.Parse(Request.Headers),ct));

    [HttpPost("copy-week"),Consumes("application/json")]
    public async Task<ActionResult<PlannerMutationDto>> CopyWeek([FromBody] CopyWeekRequest request,
        [FromHeader(Name=MealPlanRevisionHeader.HeaderName),Required] string revisionHeader,CancellationToken ct)=>
        Ok(await copy.CopyWeekAsync(auth.GetRequiredUserId(HttpContext),request,MealPlanRevisionHeader.Parse(Request.Headers),ct));
}
