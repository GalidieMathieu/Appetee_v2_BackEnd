namespace Appetee.Application.RowData.MealPlans;

public sealed record PlannerContextRowData
{
    public long Revision {get;init;}
    public int? CurrentNutritionPlanId {get;init;}
    public int? CurrentPlanId {get;init;}
    public string? CurrentPlanName {get;init;}
    public string? Mode {get;init;}
    public int? DefaultMealsPerDay {get;init;}
    public decimal? TargetCaloriesKcal {get;init;}
    public decimal? TargetProteinG {get;init;}
    public decimal? TargetCarbsG {get;init;}
    public decimal? TargetFatG {get;init;}
    public string? CalculationPolicyVersion {get;init;}
}
public sealed record PlannerDayRowData(int Id,DateTime PlanDate);
public sealed record PlannerMealRowData(int Id,int DayId,string Name,int SortOrder)
{
    // MySQL SMALLINT UNSIGNED is UInt16; property mapping performs the safe conversion to int.
    public PlannerMealRowData() : this(0,0,"",0) { }
}
public sealed record PlannerItemRowData
{
    public int Id {get;init;}
    public int MealId {get;init;}
    public string ItemType {get;init;}="";
    public int SortOrder {get;init;}
    public int? RecipeId {get;init;}
    public int? Quantity {get;init;}
    public bool Available {get;init;}
    public string? RecipeName {get;init;}
    public int? Servings {get;init;}
    public decimal? CaloriesTotal {get;init;}
    public decimal? ProteinTotal {get;init;}
    public decimal? CarbsTotal {get;init;}
    public decimal? FatTotal {get;init;}
    public int? ManualDetailId {get;init;}
    public string? ManualName {get;init;}
    public decimal? ManualCaloriesKcal {get;init;}
    public decimal? ManualProteinG {get;init;}
    public decimal? ManualCarbsG {get;init;}
    public decimal? ManualFatG {get;init;}
}
public sealed record MealPlannerReadRowData(PlannerContextRowData Context,IReadOnlyList<PlannerDayRowData> Days,
    IReadOnlyList<PlannerMealRowData> Meals,IReadOnlyList<PlannerItemRowData> Items);

/// <summary>Captured rows and identities from one complete atomic meal operation.</summary>
public sealed record PlannerMutationRowData(MealPlannerReadRowData Readback,DateOnly From,DateOnly Through,
    int? CreatedMealId,int? CreatedItemId);
