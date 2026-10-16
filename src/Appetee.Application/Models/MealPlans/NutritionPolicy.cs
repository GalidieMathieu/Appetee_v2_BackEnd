namespace Appetee.Application.Models.MealPlans;

public enum NutritionGoal { Performance, Maintain, FatLoss, MuscleGain, WeightGain, Recomposition }
public enum NutritionActivity { Inactive, LowActive, Active, VeryActive }

/// <summary>Approved Appetee operating policy, not a claim of clinical accuracy or universal safety.</summary>
public static class NutritionPolicy
{
    public const string Version = "mp-v1.0.0-2026-10";
    public const decimal WeightCoefficient = 11.936m;
    public const decimal HeightMetresCoefficient = 587.728m;
    public const decimal AgeCoefficient = -8.129m;
    public const decimal MaleCoefficient = 191.027m;
    public const decimal Intercept = 29.279m;
    public const int MinimumEnergy = 1500;
    public const int MaximumEnergy = 6000;
    public const decimal EnergyStep = 25m;
    public const decimal MinimumFatFraction = .20m;
    public const decimal CentreFatFraction = .275m;
    public const decimal MaximumFatFraction = .35m;
    public const string PopulationWarning = "equation_population_extrapolation";
    public const string CarbohydrateWarning = "carbohydrate_below_preferred";

    public static NutritionGoal? ParseGoal(string? code) => code switch
    {
        "performance" => NutritionGoal.Performance, "maintain" => NutritionGoal.Maintain,
        "fatLoss" => NutritionGoal.FatLoss, "muscleGain" => NutritionGoal.MuscleGain,
        "weightGain" => NutritionGoal.WeightGain, "recomposition" => NutritionGoal.Recomposition,
        _ => null,
    };

    public static NutritionActivity? ParseActivity(string? code) => code switch
    {
        "inactive" => NutritionActivity.Inactive, "lowActive" => NutritionActivity.LowActive,
        "active" => NutritionActivity.Active, "veryActive" => NutritionActivity.VeryActive,
        _ => null,
    };

    public static string Code(NutritionGoal goal) => goal switch
    {
        NutritionGoal.Performance => "performance", NutritionGoal.Maintain => "maintain",
        NutritionGoal.FatLoss => "fatLoss", NutritionGoal.MuscleGain => "muscleGain",
        NutritionGoal.WeightGain => "weightGain", NutritionGoal.Recomposition => "recomposition",
        _ => throw new ArgumentOutOfRangeException(nameof(goal)),
    };

    public static string Code(NutritionActivity activity) => activity switch
    {
        NutritionActivity.Inactive => "inactive", NutritionActivity.LowActive => "lowActive",
        NutritionActivity.Active => "active", NutritionActivity.VeryActive => "veryActive",
        _ => throw new ArgumentOutOfRangeException(nameof(activity)),
    };

    public static decimal ActivityFactor(NutritionActivity activity) => activity switch
    {
        NutritionActivity.Inactive => 1.40m, NutritionActivity.LowActive => 1.60m,
        NutritionActivity.Active => 1.75m, NutritionActivity.VeryActive => 2.05m,
        _ => throw new ArgumentOutOfRangeException(nameof(activity)),
    };

    public static decimal PreferredCarbsPerKg(NutritionActivity activity) => activity switch
    {
        NutritionActivity.Inactive => 3m, NutritionActivity.LowActive => 4m,
        NutritionActivity.Active => 5m, NutritionActivity.VeryActive => 6m,
        _ => throw new ArgumentOutOfRangeException(nameof(activity)),
    };

    public static decimal GoalEnergy(decimal maintenance, NutritionGoal goal) => goal switch
    {
        NutritionGoal.FatLoss => maintenance - Math.Min(.15m * maintenance, 500m),
        NutritionGoal.MuscleGain => 1.05m * maintenance,
        NutritionGoal.WeightGain => maintenance + Math.Min(.10m * maintenance, 500m),
        NutritionGoal.Performance or NutritionGoal.Maintain or NutritionGoal.Recomposition => maintenance,
        _ => throw new ArgumentOutOfRangeException(nameof(goal)),
    };

    public static decimal ProteinPerKg(NutritionGoal goal) => goal switch
    {
        NutritionGoal.FatLoss => 2m,
        NutritionGoal.Performance or NutritionGoal.Maintain or NutritionGoal.MuscleGain
            or NutritionGoal.WeightGain or NutritionGoal.Recomposition => 1.6m,
        _ => throw new ArgumentOutOfRangeException(nameof(goal)),
    };

    public static int RoundEnergy(decimal energy) => checked((int)(
        decimal.Round(energy / EnergyStep, 0, MidpointRounding.AwayFromZero) * EnergyStep));

    public static decimal RoundEstimate(decimal estimate) => decimal.Round(estimate, 2, MidpointRounding.AwayFromZero);

    public static bool CarbohydrateBelowPreferred(int carbs, decimal preferred) => carbs < preferred - 1m;
}
