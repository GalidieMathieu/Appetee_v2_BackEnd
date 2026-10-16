using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Helpers;

public sealed class NutritionPlanSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(SetCurrentNutritionPlanRequest))
        {
            schema.AdditionalPropertiesAllowed = false;
            schema.Required.Add("nutritionPlanId");
            schema.Properties["nutritionPlanId"].Minimum = 1;
            schema.Properties["nutritionPlanId"].Maximum = int.MaxValue;
        }
        if (context.Type == typeof(UpdateNutritionPlanRequest))
        {
            schema.AdditionalPropertiesAllowed = false;
            schema.Required.UnionWith(["name", "defaultMealsPerDay"]);
            schema.Properties["name"].MinLength = 1; schema.Properties["name"].MaxLength = 80;
            schema.Properties["name"].Description = "Trimmed NFC, 1–80 Unicode scalar values; no control/line characters.";
            schema.Properties["defaultMealsPerDay"].Minimum = 1; schema.Properties["defaultMealsPerDay"].Maximum = 8;
            schema.Description = "Mode is immutable. Omit all calculation/review members for metadata-only. Intermediate recalculation requires all three non-null; Easy rejects their presence.";
            schema.OneOf = [
                new() { Not = new() { AnyOf = [new() { Required = new HashSet<string> { "calculation" } },
                    new() { Required = new HashSet<string> { "reviewedPolicyVersion" } }, new() { Required = new HashSet<string> { "reviewedCalculationYear" } }] } },
                new() { Required = new HashSet<string> { "calculation", "reviewedPolicyVersion", "reviewedCalculationYear" },
                    Properties = new Dictionary<string, OpenApiSchema> {
                        ["calculation"] = new() { Reference = schema.Properties["calculation"].Reference },
                        ["reviewedPolicyVersion"] = new() { Type = "string", MinLength = 1 },
                        ["reviewedCalculationYear"] = new() { Type = "integer", Format = "int32" } } } ];
        }
        if (context.Type == typeof(CreateNutritionPlanRequest))
        {
            schema.Required.UnionWith(["name", "mode", "defaultMealsPerDay"]);
            schema.AdditionalPropertiesAllowed = false;
            schema.Properties["name"].MinLength = 1; schema.Properties["name"].MaxLength = 80;
            schema.Properties["name"].Description = "Trimmed NFC plain text, 1–80 Unicode scalar values, no line/control characters.";
            schema.Properties["mode"].Enum = [new OpenApiString("easy"), new OpenApiString("intermediate")];
            schema.Properties["defaultMealsPerDay"].Minimum = 1; schema.Properties["defaultMealsPerDay"].Maximum = 8;
            schema.Description = "Easy omits calculation and both review fields (including null). Intermediate requires all three; server recomputes and compares review policy/year.";
            schema.OneOf =
            [
                new OpenApiSchema
                {
                    Properties = new Dictionary<string, OpenApiSchema>
                    { ["mode"] = new() { Type = "string", Enum = [new OpenApiString("easy")] } },
                    Not = new OpenApiSchema { AnyOf = [
                        new() { Required = new HashSet<string> { "calculation" } },
                        new() { Required = new HashSet<string> { "reviewedPolicyVersion" } },
                        new() { Required = new HashSet<string> { "reviewedCalculationYear" } } ] },
                },
                new OpenApiSchema
                {
                    Required = new HashSet<string> { "calculation", "reviewedPolicyVersion", "reviewedCalculationYear" },
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["mode"] = new() { Type = "string", Enum = [new OpenApiString("intermediate")] },
                        ["calculation"] = new() { Reference = schema.Properties["calculation"].Reference },
                        ["reviewedPolicyVersion"] = new() { Type = "string", MinLength = 1 },
                        ["reviewedCalculationYear"] = new() { Type = "integer", Format = "int32" },
                    },
                },
            ];
        }
        if (context.Type == typeof(NutritionPlanListDto) || context.Type == typeof(NutritionPlanDetailDto)
            || context.Type == typeof(NutritionPlanMutationDto))
        {
            var revision = schema.Properties["revision"]; revision.Type = "string"; revision.Format = null; revision.Pattern = "^[0-9]+$";
            schema.Required.UnionWith(schema.Properties.Keys);
        }
        if (context.Type == typeof(NutritionPlanSummaryDto) || context.Type == typeof(NutritionPlanDto)
            || context.Type == typeof(CurrentNutritionPlanDto) || context.Type == typeof(MealPlanSettingsDto))
        {
            schema.Required.UnionWith(schema.Properties.Keys);
            if (schema.Properties.TryGetValue("mode", out var mode))
                mode.Enum = [new OpenApiString("easy"), new OpenApiString("intermediate")];
        }
    }
}
