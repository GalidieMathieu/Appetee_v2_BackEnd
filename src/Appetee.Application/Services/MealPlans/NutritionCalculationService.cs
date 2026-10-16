using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Services.MealPlans;

/// <summary>Validates explicit submitted inputs using server UTC year; preview never accesses persistence.</summary>
public sealed class NutritionCalculationService(TimeProvider timeProvider) : INutritionCalculationService
{
    public NutritionCalculationDto Calculate(NutritionCalculationRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var year = timeProvider.GetUtcNow().UtcDateTime.Year;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.Profile is null) errors["profile"] = ["A complete nutrition profile is required."];
        else
        {
            try { MealPlanRules.ValidateNutritionProfile(request.Profile, year); }
            catch (ValidationException ex) when (ex.Errors is not null)
            {
                foreach (var entry in ex.Errors) errors[$"profile.{entry.Key}"] = entry.Value;
            }
        }
        var goal = NutritionPolicy.ParseGoal(request.Goal);
        var activity = NutritionPolicy.ParseActivity(request.ActivityLevel);
        if (goal is null) errors["goal"] = ["Choose a supported goal code."];
        if (activity is null) errors["activityLevel"] = ["Choose a supported activity code."];
        if (errors.Count > 0)
            throw new ValidationException("Nutrition calculation validation failed.", "validation_failed", errors);
        var profile = request.Profile!;
        var input = new NutritionCalculationInputs(profile.CalculationSex, profile.BirthYear,
            year - profile.BirthYear, profile.HeightCm, profile.WeightKg, goal!.Value, activity!.Value);
        var result = NutritionCalculator.Calculate(input);
        var targets = result.Targets;
        return new(NutritionPolicy.Version, year,
            new(profile.CalculationSex, profile.BirthYear, input.AgeYears, profile.HeightCm, profile.WeightKg,
                NutritionPolicy.Code(input.Goal), NutritionPolicy.Code(input.ActivityLevel)),
            NutritionPolicy.RoundEstimate(result.RmrKcal), NutritionPolicy.RoundEstimate(result.MaintenanceKcal),
            new(targets.CaloriesKcal, targets.ProteinG, targets.CarbsG, targets.FatG), result.Warnings);
    }
}
