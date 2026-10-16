using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Services.MealPlans;

public static class NutritionPlanMapper
{
    public static NutritionPlanSummaryDto Summary(NutritionPlanRowData row) => new()
    {
        Id = row.Id, Name = row.Name, Mode = Mode(row.Mode), DefaultMealsPerDay = row.DefaultMealsPerDay,
        Goal = row.Goal, ActivityLevel = row.ActivityLevel, Targets = Targets(row),
        CalculationPolicyVersion = row.CalculationPolicyVersion, CalculatedAt = Utc(row.CalculatedAt),
        CreatedAt = Utc(row.CreatedAt)!.Value, UpdatedAt = Utc(row.UpdatedAt)!.Value,
    };

    public static NutritionPlanDto Detail(NutritionPlanRowData row)
    {
        var summary = Summary(row);
        CalculationInputsDto? inputs = null;
        var warnings = new List<string>();
        if (row.Mode == "Intermediate")
        {
            if (row.CalculationSex is null || row.BirthYear is null || row.AgeYears is null
                || row.HeightCm is null || row.WeightKg is null || row.Goal is null || row.ActivityLevel is null)
                throw new InternalServerException("The stored calculation is incomplete.");
            inputs = new(row.CalculationSex, row.BirthYear.Value, row.AgeYears.Value,
                row.HeightCm.Value, row.WeightKg.Value, row.Goal, row.ActivityLevel);
            if (inputs.AgeYears > 35 || inputs.ActivityLevel == "inactive") warnings.Add(NutritionPolicy.PopulationWarning);
            var activity = NutritionPolicy.ParseActivity(inputs.ActivityLevel)
                ?? throw new InternalServerException("The stored activity code is invalid.");
            if (row.TargetCarbsG is not null && NutritionPolicy.CarbohydrateBelowPreferred(
                checked((int)row.TargetCarbsG.Value), inputs.WeightKg * NutritionPolicy.PreferredCarbsPerKg(activity)))
                warnings.Add(NutritionPolicy.CarbohydrateWarning);
        }
        return new()
        {
            Id = summary.Id, Name = summary.Name, Mode = summary.Mode, DefaultMealsPerDay = summary.DefaultMealsPerDay,
            Goal = summary.Goal, ActivityLevel = summary.ActivityLevel, Targets = summary.Targets,
            CalculationPolicyVersion = summary.CalculationPolicyVersion, CalculatedAt = summary.CalculatedAt,
            CreatedAt = summary.CreatedAt, UpdatedAt = summary.UpdatedAt, CalculationInputs = inputs,
            RmrKcal = row.RmrKcal, MaintenanceKcal = row.MaintenanceKcal, Warnings = warnings.ToArray(),
        };
    }

    public static MealPlanSettingsDto Settings(NutritionPlanReadRowData rows)
    {
        if (rows.Context.CurrentNutritionPlanId is null) return new(null, null);
        var current = rows.CurrentPlan;
        if (current is null || current.Id != rows.Context.CurrentNutritionPlanId)
            throw new InternalServerException("The current nutrition plan context is unavailable.");
        return new(current.Id, new(current.Id, current.Name, Mode(current.Mode), current.DefaultMealsPerDay,
            Targets(current), current.CalculationPolicyVersion));
    }

    public static bool ProfileDiffers(NutritionPlanRowData plan, NutritionPlanContextRowData profile) =>
        plan.Mode == "Intermediate" && (plan.CalculationSex != profile.CalculationSex || plan.BirthYear != profile.BirthYear
            || plan.HeightCm != profile.HeightCm || plan.WeightKg != profile.WeightKg);

    public static NutritionProfileValue Profile(NutritionPlanContextRowData profile) =>
        profile.CalculationSex is not null && profile.BirthYear is not null && profile.HeightCm is not null && profile.WeightKg is not null
            ? new(profile.CalculationSex, profile.BirthYear.Value, profile.HeightCm.Value, profile.WeightKg.Value)
            : throw new InternalServerException("The saved nutrition profile is incomplete.");

    public static ApiException Unavailable() => new NutritionPlanNotFoundException();
    private sealed class NutritionPlanNotFoundException() : ApiException(404, "Nutrition plan is not available.", code: "resource_not_found");

    private static string Mode(string mode) => mode switch
    { "Easy" => "easy", "Intermediate" => "intermediate", _ => throw new InternalServerException("The stored plan mode is invalid.") };
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    private static NutrientsDto? Targets(NutritionPlanRowData row) => row.Mode == "Easy" ? null :
        new(row.TargetCaloriesKcal, row.TargetProteinG, row.TargetCarbsG, row.TargetFatG);
}
