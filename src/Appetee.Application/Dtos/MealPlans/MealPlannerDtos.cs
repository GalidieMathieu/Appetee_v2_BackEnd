using System.Text.Json.Serialization;

namespace Appetee.Application.Dtos.MealPlans;

public sealed record MealPlannerRangeDto(string Revision,MealPlanSettingsDto Settings,DateOnly From,DateOnly Through,IReadOnlyList<PlannerDayDto> Days);
public sealed record PlannerMutationDto(string Revision,MealPlanSettingsDto Settings,IReadOnlyList<PlannerDayDto> Days,int? CreatedMealId,int? CreatedItemId);
public sealed record PlannerDayDto(DateOnly Date,int? Id,string Persistence,int SavedMealCount,IReadOnlyList<PlannerMealDto> Meals,
    NutrientsDto Totals,NutrientsDto? Targets,NutrientsDto? Remaining,IReadOnlyList<string> Warnings);
public sealed record PlannerMealDto(int? Id,string Key,bool IsVirtual,int? DefaultSlot,string Name,int SortOrder,
    IReadOnlyList<PlannerItemDto> Items,NutrientsDto Totals,IReadOnlyList<string> Warnings);

[JsonPolymorphic(TypeDiscriminatorPropertyName="type")]
[JsonDerivedType(typeof(PlannerRecipeItemDto),"recipe")]
[JsonDerivedType(typeof(PlannerManualItemDto),"manual")]
public abstract record PlannerItemDto(int Id,int SortOrder,NutrientsDto Nutrition);
public sealed record PlannerRecipeItemDto(int Id,int SortOrder,int RecipeId,int Quantity,string Availability,string? Name,
    NutrientsDto? PerServing,NutrientsDto Nutrition,IReadOnlyList<string> Warnings) : PlannerItemDto(Id,SortOrder,Nutrition);
public sealed record PlannerManualItemDto(int Id,int SortOrder,string Name,NutrientsDto Nutrition) : PlannerItemDto(Id,SortOrder,Nutrition);
