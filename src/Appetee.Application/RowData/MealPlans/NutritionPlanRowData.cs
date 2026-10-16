using Appetee.Application.Models.MealPlans;

namespace Appetee.Application.RowData.MealPlans;

public sealed record NutritionPlanRowData
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Mode { get; init; } = "";
    public int DefaultMealsPerDay { get; init; }
    public string? Goal { get; init; }
    public string? ActivityLevel { get; init; }
    public string? CalculationSex { get; init; }
    public int? BirthYear { get; init; }
    public int? AgeYears { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? WeightKg { get; init; }
    public string? CalculationPolicyVersion { get; init; }
    public decimal? RmrKcal { get; init; }
    public decimal? MaintenanceKcal { get; init; }
    public decimal? TargetCaloriesKcal { get; init; }
    public decimal? TargetProteinG { get; init; }
    public decimal? TargetCarbsG { get; init; }
    public decimal? TargetFatG { get; init; }
    public DateTime? CalculatedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record NutritionPlanContextRowData
{
    public long Revision { get; init; }
    public int? CurrentNutritionPlanId { get; init; }
    public string? CalculationSex { get; init; }
    public int? BirthYear { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? WeightKg { get; init; }
}

public sealed record NutritionPlanReadRowData(NutritionPlanContextRowData Context,
    NutritionPlanRowData? CurrentPlan, IReadOnlyList<NutritionPlanRowData> Plans);
public sealed record NutritionPlanCreateData(string Name, string Mode, int DefaultMealsPerDay,
    NutritionCalculationResult? Calculation, string? PolicyVersion, int? CalculationYear);
public sealed record NutritionPlanCreatedRowData(NutritionPlanReadRowData Readback,
    bool ProfileChanged, bool PlannerInvalidated);
public sealed record NutritionPlanUpdateData(string Name, int DefaultMealsPerDay,
    NutritionCalculationResult? Calculation, string? PolicyVersion, int? CalculationYear);
public sealed record NutritionPlanMutationRowData(NutritionPlanReadRowData Readback,
    int? DeletedPlanId, bool ProfileChanged, bool PlannerInvalidated);
