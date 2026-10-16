using Appetee.Application.utils;

namespace Appetee.Application.Models.MealPlans;

public sealed record NutritionCalculationInputs(
    string CalculationSex, int BirthYear, int AgeYears, decimal HeightCm, decimal WeightKg,
    NutritionGoal Goal, NutritionActivity ActivityLevel);

public sealed record NutritionTargets(int CaloriesKcal, int ProteinG, int CarbsG, int FatG);

public sealed record NutritionCalculationResult(
    NutritionCalculationInputs Inputs, decimal RmrKcal, decimal MaintenanceKcal,
    NutritionTargets Targets, IReadOnlyList<string> Warnings);

/// <summary>Pure arithmetic on validated inputs; no identity, clock, repository, or transport dependency.</summary>
public static class NutritionCalculator
{
    public static NutritionCalculationResult Calculate(NutritionCalculationInputs inputs)
    {
        var rmr = NutritionPolicy.WeightCoefficient * inputs.WeightKg
            + NutritionPolicy.HeightMetresCoefficient * (inputs.HeightCm / 100m)
            + NutritionPolicy.AgeCoefficient * inputs.AgeYears
            + (inputs.CalculationSex == "male" ? NutritionPolicy.MaleCoefficient : 0m)
            + NutritionPolicy.Intercept;
        var maintenance = rmr * NutritionPolicy.ActivityFactor(inputs.ActivityLevel);
        var energy = NutritionPolicy.RoundEnergy(NutritionPolicy.GoalEnergy(maintenance, inputs.Goal));
        var targets = CalculateTargets(energy, inputs.WeightKg, inputs.Goal, inputs.ActivityLevel);
        var warnings = new List<string>(2);
        if (inputs.AgeYears > 35 || inputs.ActivityLevel == NutritionActivity.Inactive)
            warnings.Add(NutritionPolicy.PopulationWarning);
        var preferred = inputs.WeightKg * NutritionPolicy.PreferredCarbsPerKg(inputs.ActivityLevel);
        if (NutritionPolicy.CarbohydrateBelowPreferred(targets.CarbsG, preferred))
            warnings.Add(NutritionPolicy.CarbohydrateWarning);
        return new(inputs, rmr, maintenance, targets, warnings.ToArray());
    }

    public static NutritionTargets CalculateTargets(
        int energy, decimal weightKg, NutritionGoal goal, NutritionActivity activity)
    {
        if (energy < NutritionPolicy.MinimumEnergy || energy > NutritionPolicy.MaximumEnergy)
            throw Error("calculation_outside_supported_range", "The estimate is outside the supported calculation range.");
        var protein = checked((int)decimal.Ceiling(NutritionPolicy.ProteinPerKg(goal) * weightKg));
        var preferred = weightKg * NutritionPolicy.PreferredCarbsPerKg(activity);
        var fatMin = NutritionPolicy.MinimumFatFraction * energy / 9m;
        var fatCentre = NutritionPolicy.CentreFatFraction * energy / 9m;
        var fatMax = NutritionPolicy.MaximumFatFraction * energy / 9m;
        if (4m * protein + 9m * fatMin >= energy)
            throw Error("macro_feasibility_conflict", "The requested calculation cannot produce feasible macro targets.");
        var ideal = Math.Max(fatMin, Math.Min(fatCentre, (energy - 4m * protein - 4m * preferred) / 9m));
        var fat = checked((int)Math.Clamp(decimal.Round(ideal, 0, MidpointRounding.AwayFromZero),
            decimal.Ceiling(fatMin), decimal.Floor(fatMax)));
        var carbs = checked((int)decimal.Round((energy - 4m * protein - 9m * fat) / 4m,
            0, MidpointRounding.AwayFromZero));
        if (energy % 25 != 0 || protein <= 0 || fat <= 0 || carbs <= 0
            || 9m * fat < NutritionPolicy.MinimumFatFraction * energy
            || 9m * fat > NutritionPolicy.MaximumFatFraction * energy
            || Math.Abs(4m * protein + 4m * carbs + 9m * fat - energy) > 2m)
            throw Error("macro_feasibility_conflict", "The requested calculation cannot produce feasible macro targets.");
        return new(energy, protein, carbs, fat);
    }

    private static ValidationException Error(string code, string message) =>
        new(message, code, new Dictionary<string, string[]> { ["calculation"] = [message] });
}
