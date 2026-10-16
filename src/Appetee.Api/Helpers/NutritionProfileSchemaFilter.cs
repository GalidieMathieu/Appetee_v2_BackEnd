using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Helpers;

/// <summary>Aligns this bounded contract with its strict JSON and decimal-string revision wire shape.</summary>
public sealed class NutritionProfileSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(NutritionProfileDto))
        {
            schema.Required.UnionWith(["revision", "profile"]);
            var revision = schema.Properties["revision"];
            revision.Type = "string";
            revision.Format = null;
            revision.Pattern = "^[0-9]+$";
            revision.Description = "Nonnegative signed-BIGINT revision serialized as an exact decimal string.";
            schema.Properties["profile"].Nullable = true;
        }
        if (context.Type != typeof(NutritionProfileRequest) && context.Type != typeof(NutritionProfileValue)) return;
        schema.Required.UnionWith(["calculationSex", "birthYear", "heightCm", "weightKg"]);
        schema.Properties["calculationSex"].Enum = [new OpenApiString("male"), new OpenApiString("female")];
        schema.Properties["birthYear"].Minimum = 1900;
        schema.Properties["birthYear"].Maximum = 9999;
        schema.Properties["birthYear"].Description = "On PUT, age in the current server UTC year must be 18–65.";
        schema.Properties["heightCm"].Minimum = 120;
        schema.Properties["heightCm"].Maximum = 230;
        schema.Properties["heightCm"].MultipleOf = .01m;
        schema.Properties["weightKg"].Minimum = 35;
        schema.Properties["weightKg"].Maximum = 250;
        schema.Properties["weightKg"].MultipleOf = .01m;
        if (context.Type == typeof(NutritionProfileRequest))
        {
            schema.AdditionalPropertiesAllowed = false;
            schema.Properties["adultConfirmed"].Description = "Request-only declaration; must be true when age this UTC year is 18.";
        }
    }
}
