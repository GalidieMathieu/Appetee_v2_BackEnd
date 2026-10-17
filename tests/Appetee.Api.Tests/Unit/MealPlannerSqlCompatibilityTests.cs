using Appetee.Infrastructure.MealPlans;

namespace Appetee.Api.Tests.Unit;

/// <summary>Source-level regressions for the MySQL 8.4 reserved-alias incident; not a server parser test.</summary>
public sealed class MealPlannerSqlCompatibilityTests
{
    [Fact]
    public void ItemsUsesNonreservedManualItemAlias()
    {
        Assert.DoesNotMatch(@"(?i)\bmanual\b", MealPlannerSql.Items);
        Assert.Contains("LEFT JOIN meal_plan_manual_items AS manual_item ON manual_item.meal_plan_meal_item_id=i.id", MealPlannerSql.Items);
    }

    [Theory]
    [InlineData("meal_plan_meal_item_id", "ManualDetailId")]
    [InlineData("name", "ManualName")]
    [InlineData("calories_kcal", "ManualCaloriesKcal")]
    [InlineData("protein_g", "ManualProteinG")]
    [InlineData("carbs_g", "ManualCarbsG")]
    [InlineData("fat_g", "ManualFatG")]
    public void AliasRenamePreservesManualProjectionContract(string column, string projection)
    {
        Assert.Contains($"manual_item.{column} {projection}", MealPlannerSql.Items);
    }

    [Fact]
    public void RangeRetainsFourResultsOwnerDateAndCompatibilityFilters()
    {
        Assert.Equal(MealPlannerSql.Context + MealPlannerSql.Days + MealPlannerSql.Meals + MealPlannerSql.Items, MealPlannerSql.Range);
        Assert.Equal(4, MealPlannerSql.Range.Count(character => character == ';'));
        Assert.Contains("d.user_id=@CurrentUserId AND d.plan_date BETWEEN @From AND @Through", MealPlannerSql.Items);
        Assert.Contains(RecipeCompatibilitySql.Predicate, MealPlannerSql.Items);
        Assert.Contains("ORDER BY d.plan_date,m.sort_order,m.id,i.sort_order,i.id;", MealPlannerSql.Items);
    }
}
