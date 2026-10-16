using Appetee.Api.Helpers;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.MealPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Appetee.Application.utils;
using System.Diagnostics;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;

namespace Appetee.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/nutrition-plans")]
public sealed class NutritionPlansController(
    INutritionCalculationService calculations, IAuthService authService,
    ILogger<NutritionPlansController> logger, INutritionPlanService plans) : ControllerBase
{
    [HttpGet]
    [NutritionPlanBoundary]
    public async Task<ActionResult<NutritionPlanListDto>> List(CancellationToken ct) =>
        Ok(await plans.ListAsync(authService.GetRequiredUserId(HttpContext), ct));

    [HttpGet("{planId:int}")]
    [NutritionPlanBoundary]
    public async Task<ActionResult<NutritionPlanDetailDto>> Get(int planId, CancellationToken ct) =>
        Ok(await plans.GetAsync(authService.GetRequiredUserId(HttpContext), planId, ct));

    [HttpPost]
    [Consumes("application/json")]
    [NutritionPlanBoundary]
    [ProducesResponseType(typeof(NutritionPlanMutationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<NutritionPlanMutationDto>> Create([FromBody] CreateNutritionPlanRequest request,
        [FromHeader(Name = MealPlanRevisionHeader.HeaderName), Required] string revisionHeader, CancellationToken ct)
    {
        var result = await plans.CreateAsync(authService.GetRequiredUserId(HttpContext), request,
            MealPlanRevisionHeader.Parse(Request.Headers), ct);
        return Created($"/api/nutrition-plans/{result.Plan!.Id}", result);
    }

    [HttpPut("{planId:int}")]
    [Consumes("application/json")]
    [NutritionPlanBoundary]
    public async Task<ActionResult<NutritionPlanMutationDto>> Update(int planId, [FromBody] UpdateNutritionPlanRequest request,
        [FromHeader(Name = MealPlanRevisionHeader.HeaderName), Required] string revisionHeader, CancellationToken ct) =>
        Ok(await plans.UpdateAsync(authService.GetRequiredUserId(HttpContext), planId, request, MealPlanRevisionHeader.Parse(Request.Headers), ct));

    [HttpPost("{planId:int}/duplicate")]
    [NutritionPlanBoundary(AcceptsBody = false)]
    [ProducesResponseType(typeof(NutritionPlanMutationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<NutritionPlanMutationDto>> Duplicate(int planId,
        [FromHeader(Name = MealPlanRevisionHeader.HeaderName), Required] string revisionHeader, CancellationToken ct)
    {
        var result = await plans.DuplicateAsync(authService.GetRequiredUserId(HttpContext), planId, MealPlanRevisionHeader.Parse(Request.Headers), ct);
        return Created($"/api/nutrition-plans/{result.Plan!.Id}", result);
    }

    [HttpDelete("{planId:int}")]
    [NutritionPlanBoundary(AcceptsBody = false)]
    public async Task<ActionResult<NutritionPlanMutationDto>> Delete(int planId,
        [FromHeader(Name = MealPlanRevisionHeader.HeaderName), Required] string revisionHeader, CancellationToken ct) =>
        Ok(await plans.DeleteAsync(authService.GetRequiredUserId(HttpContext), planId, MealPlanRevisionHeader.Parse(Request.Headers), ct));

    [HttpPost("calculation-preview")]
    [Consumes("application/json")]
    [NutritionCalculationBoundary]
    [ProducesResponseType(typeof(NutritionCalculationDto), StatusCodes.Status200OK)]
    public ActionResult<NutritionCalculationDto> CalculationPreview(
        [FromBody] NutritionCalculationRequest request, CancellationToken ct)
    {
        _ = authService.GetRequiredUserId(HttpContext);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        try
        {
            var result = calculations.Calculate(request, ct);
            outcome = "completed";
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            outcome = ex.Code ?? "validation_failed";
            throw;
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            // Only operation metadata, never profile values, submitted bodies, or tokens.
            logger.LogInformation(Appetee.Application.Diagnostics.LogEvents.NutritionPreview, "Nutrition calculation preview outcome {Outcome}; elapsed {ElapsedMs} ms; rows read {RowsRead}; rows written {RowsWritten}; trace {TraceId}",
                outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds, 0, 0, HttpContext.TraceIdentifier);
        }
    }
}
