using Appetee.Application.utils;
using Appetee.Application.Dtos.MealPlans;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Diagnostics;

namespace Appetee.Api.Helpers;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NutritionPlanBoundaryAttribute : ActionFilterAttribute
{
    public NutritionPlanBoundaryAttribute() => Order = -3001;
    public bool AcceptsBody { get; set; } = true;
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var started = Stopwatch.GetTimestamp();
        var status = 500;
        var outcome = "failed";
        var planRows = 0;
        var profileChanged = false;
        var action = context.RouteData.Values["action"]?.ToString();
        var operation = action switch
        {
            "Create" => "nutrition-plan.create", "Update" => "nutrition-plan.update",
            "Duplicate" => "nutrition-plan.duplicate", "Delete" => "nutrition-plan.delete",
            "SetCurrent" => "nutrition-plan.set-current", "Get" => "nutrition-plan.detail", _ => "nutrition-plan.list",
        };
        try
        {
            OnActionExecuting(context);
            var executed = await next();
            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                if (executed.Exception is ApiException api) { status = api.StatusCode; outcome = api.Code ?? "failed"; }
                else if (executed.Exception is OperationCanceledException) outcome = "cancelled";
                else
                {
                    // Development error formatting also must not expose SQL/profile details.
                    // Keep public details generic, but preserve the original cause for privacy-safe diagnostics.
                    executed.Exception = new InternalServerException(inner: executed.Exception);
                }
            }
            else
            {
                var result = executed.Result as ObjectResult;
                status = result?.StatusCode ?? (executed.Result is StatusCodeResult explicitStatus
                    ? explicitStatus.StatusCode : context.HttpContext.Response.StatusCode);
                outcome = status < 400 ? "completed" : "rejected";
                planRows = result?.Value switch
                { NutritionPlanListDto list => list.Plans.Count, NutritionPlanDetailDto => 1,
                  NutritionPlanMutationDto { Plan: not null } => 1, _ => 0 };
                profileChanged = result?.Value is NutritionPlanMutationDto { Profile: not null };
            }
        }
        catch (ApiException api) { status = api.StatusCode; outcome = api.Code ?? "failed"; throw; }
        catch (OperationCanceledException) { outcome = "cancelled"; throw; }
        catch (Exception exception) { throw new InternalServerException(inner: exception); }
        finally
        {
            // Logical result-row counts only; never names, IDs, bodies, measurements, headers, or SQL.
            context.HttpContext.RequestServices.GetRequiredService<ILogger<NutritionPlanBoundaryAttribute>>()
                .LogInformation(Appetee.Application.Diagnostics.LogEvents.NutritionPlanOperation, "Meal Plan API operation {Operation}; status {Status}; outcome {Outcome}; elapsed {ElapsedMs} ms; plan rows {PlanRows}; profile changed {ProfileChanged}; trace {TraceId}",
                    operation, status, outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    planRows, profileChanged, context.HttpContext.TraceIdentifier);
        }
    }
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.Query.Count > 0) throw Invalid("query", "Query parameters are not supported.");
        if ((!AcceptsBody || HttpMethods.IsGet(request.Method)) && (request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding")))
            throw Invalid("request", "This operation does not accept a body.");
        if (context.ModelState.IsValid) return;
        if (context.ModelState.Values.SelectMany(v => v.Errors).Any(e => e.Exception is UnsupportedContentTypeException)) return;
        if (!HttpMethods.IsGet(request.Method)) _ = MealPlanRevisionHeader.Parse(request.Headers);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var entry in context.ModelState.Where(e => e.Value!.Errors.Count > 0))
        {
            var key = entry.Key.TrimStart('$', '.');
            if (key.Length == 0 || key == "request") key = "request";
            key = string.Join('.', key.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
            errors[key] = ["The field is missing or invalid."];
        }
        throw new ValidationException("Nutrition plan validation failed.", "validation_failed", errors);
    }
    private static ValidationException Invalid(string key, string message) =>
        new("Nutrition plan validation failed.", "validation_failed", new Dictionary<string, string[]> { [key] = [message] });
}
