using Appetee.Application.utils;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Appetee.Api.Helpers;

/// <summary>Scopes strict binding/error normalization to the separate nutrition-profile contract.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NutritionProfileBoundaryAttribute : ActionFilterAttribute
{
    public NutritionProfileBoundaryAttribute() => Order = -3001;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Query.Count > 0)
            throw Invalid(new Dictionary<string, string[]> { ["query"] = ["Query parameters are not supported."] });
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            && (context.HttpContext.Request.ContentLength > 0
                || context.HttpContext.Request.Headers.ContainsKey("Transfer-Encoding")))
            throw Invalid(new Dictionary<string, string[]> { ["profile"] = ["This GET does not accept a request body."] });
        if (context.ModelState.IsValid) return;
        // Preserve MVC's unsupported-media-type handling instead of rewriting it to validation 400.
        if (context.ModelState.Values.SelectMany(v => v.Errors)
            .Any(e => e.Exception is UnsupportedContentTypeException)) return;
        if (HttpMethods.IsPut(context.HttpContext.Request.Method))
            _ = MealPlanRevisionHeader.Parse(context.HttpContext.Request.Headers);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var entry in context.ModelState.Where(e => e.Value!.Errors.Count > 0))
        {
            var key = entry.Key.TrimStart('$', '.');
            if (key.Length == 0 || key == "request") key = "profile";
            key = char.ToLowerInvariant(key[0]) + key[1..];
            // Framework messages can embed submitted values; use a value-free field error.
            errors[key] = ["The field is missing or invalid."];
        }
        throw Invalid(errors);
    }

    private static ValidationException Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("Nutrition profile validation failed.", "validation_failed", errors);
}
