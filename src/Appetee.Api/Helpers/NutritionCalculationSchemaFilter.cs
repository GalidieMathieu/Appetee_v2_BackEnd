using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Helpers;

public sealed class NutritionCalculationSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(NutritionCalculationRequest))
        {
            schema.Required.UnionWith(["profile", "goal", "activityLevel"]);
            schema.AdditionalPropertiesAllowed = false;
        }
        if (context.Type == typeof(CalculationInputsDto))
        {
            schema.Required.UnionWith(["calculationSex", "birthYear", "ageYears", "heightCm", "weightKg", "goal", "activityLevel"]);
            schema.Properties["calculationSex"].Enum = [new OpenApiString("male"), new OpenApiString("female")];
            schema.Properties["ageYears"].Minimum = 18;
            schema.Properties["ageYears"].Maximum = 65;
            schema.Properties["heightCm"].Minimum = 120;
            schema.Properties["heightCm"].Maximum = 230;
            schema.Properties["heightCm"].MultipleOf = .01m;
            schema.Properties["weightKg"].Minimum = 35;
            schema.Properties["weightKg"].Maximum = 250;
            schema.Properties["weightKg"].MultipleOf = .01m;
        }
        if (context.Type == typeof(NutritionCalculationRequest) || context.Type == typeof(CalculationInputsDto))
        {
            schema.Properties["goal"].Enum = Enum.GetValues<NutritionGoal>()
                .Select(goal => (IOpenApiAny)new OpenApiString(NutritionPolicy.Code(goal))).ToList();
            schema.Properties["activityLevel"].Enum = Enum.GetValues<NutritionActivity>()
                .Select(activity => (IOpenApiAny)new OpenApiString(NutritionPolicy.Code(activity))).ToList();
        }
        if (context.Type == typeof(NutritionCalculationDto))
        {
            schema.Required.UnionWith(["policyVersion", "calculationYear", "inputs", "rmrKcal", "maintenanceKcal", "targets", "warnings"]);
            schema.Properties["targets"].Description = "All four targets are known positive whole units; calories are a multiple of 25.";
            schema.Properties["rmrKcal"].MultipleOf = .01m;
            schema.Properties["maintenanceKcal"].MultipleOf = .01m;
        }
        if (context.Type == typeof(NutrientsDto))
            schema.Required.UnionWith(["caloriesKcal", "proteinG", "carbsG", "fatG"]);
    }
}
