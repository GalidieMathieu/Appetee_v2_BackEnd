using System.Text.Json.Serialization;

namespace Appetee.Application.Dtos.MealPlans;

public sealed record CurrentNutritionPlanDto(int Id, string Name, string Mode, int DefaultMealsPerDay,
    NutrientsDto? Targets, string? CalculationPolicyVersion);
public sealed record MealPlanSettingsDto(int? CurrentNutritionPlanId, CurrentNutritionPlanDto? CurrentPlan);

public record NutritionPlanSummaryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Mode { get; init; }
    public int DefaultMealsPerDay { get; init; }
    public string? Goal { get; init; }
    public string? ActivityLevel { get; init; }
    public NutrientsDto? Targets { get; init; }
    public string? CalculationPolicyVersion { get; init; }
    public DateTime? CalculatedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record NutritionPlanDto : NutritionPlanSummaryDto
{
    public CalculationInputsDto? CalculationInputs { get; init; }
    public decimal? RmrKcal { get; init; }
    public decimal? MaintenanceKcal { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record NutritionPlanListDto(
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Revision,
    MealPlanSettingsDto Settings, IReadOnlyList<NutritionPlanSummaryDto> Plans);
public sealed record NutritionPlanDetailDto(
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Revision,
    MealPlanSettingsDto Settings, NutritionPlanDto Plan, bool ProfileDiffers);
public sealed record NutritionPlanMutationDto(
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Revision,
    MealPlanSettingsDto Settings, NutritionPlanDto? Plan, int? DeletedPlanId,
    NutritionProfileValue? Profile, bool PlannerInvalidated);
