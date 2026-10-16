using Appetee.Api.Helpers;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.MealPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Appetee.Api.Controllers;

[ApiController, Authorize]
[Route("api/meal-plan")]
public sealed class MealPlanSettingsController(INutritionPlanService plans, IAuthService auth) : ControllerBase
{
    [HttpPut("current-nutrition-plan")]
    [Consumes("application/json")]
    [NutritionPlanBoundary]
    public async Task<ActionResult<NutritionPlanMutationDto>> SetCurrent([FromBody] SetCurrentNutritionPlanRequest request,
        [FromHeader(Name = MealPlanRevisionHeader.HeaderName), Required] string revisionHeader, CancellationToken ct) =>
        Ok(await plans.SetCurrentAsync(auth.GetRequiredUserId(HttpContext), request, MealPlanRevisionHeader.Parse(Request.Headers), ct));
}
