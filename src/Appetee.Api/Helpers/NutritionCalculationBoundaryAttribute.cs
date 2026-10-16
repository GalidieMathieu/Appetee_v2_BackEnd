using Appetee.Application.utils;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Appetee.Api.Helpers;

/// <summary>Normalizes strict preview binding errors without changing unrelated API contracts.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NutritionCalculationBoundaryAttribute : ActionFilterAttribute
{
    public NutritionCalculationBoundaryAttribute() => Order = -3001;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Query.Count > 0)
            throw Invalid(new Dictionary<string, string[]> { ["query"] = ["Query parameters are not supported."] });
        if (context.ModelState.IsValid) return;
        if (context.ModelState.Values.SelectMany(v => v.Errors)
            .Any(e => e.Exception is UnsupportedContentTypeException)) return;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var entry in context.ModelState.Where(e => e.Value!.Errors.Count > 0))
        {
            var key = entry.Key.TrimStart('$', '.');
            if (key.Length == 0 || key == "request") key = "calculation";
            key = string.Join('.', key.Split('.').Select(part =>
                part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
            errors[key] = ["The field is missing or invalid."];
        }
        throw Invalid(errors);
    }

    private static ValidationException Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("Nutrition calculation validation failed.", "validation_failed", errors);
}
