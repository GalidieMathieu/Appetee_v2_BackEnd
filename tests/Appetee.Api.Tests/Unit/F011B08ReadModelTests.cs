using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Services.MealPlans;
using Appetee.Application.utils;
using System.Text.Json;
using System.Globalization;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B08ReadModelTests
{
    private static readonly DateOnly Date=new(2026,10,12);
    [Theory]
    [InlineData(1,"Lunch")][InlineData(2,"Dinner")][InlineData(3,"Dinner")][InlineData(4,"Dinner")]
    [InlineData(5,"Dinner")][InlineData(6,"Evening Snack")][InlineData(7,"Late Snack")][InlineData(8,"Extra Meal")]
    public void Defaults_ProduceExactVirtualIdentities_WithoutSavedCount(int count,string lastName)
    {
        var rows=new MealPlannerReadRowData(new(){CurrentNutritionPlanId=1,CurrentPlanId=1,CurrentPlanName="Synthetic",Mode="Easy",DefaultMealsPerDay=count},[],[],[]);
        var day=MealPlanReadModelBuilder.Build(rows,Date,Date).Days[0];
        Assert.Equal(count,day.Meals.Count); Assert.Equal(lastName,day.Meals[^1].Name); Assert.Equal(0,day.SavedMealCount);
        Assert.Null(day.Id); Assert.Null(day.Targets); Assert.Null(day.Remaining);
        foreach(var meal in day.Meals)
        {
            Assert.Null(meal.Id); Assert.True(meal.IsVirtual);
            Assert.Equal($"virtual:2026-10-12:{meal.SortOrder}",meal.Key);
            Assert.Equal(MealPlanNutrition.Zero,meal.Totals);
        }
    }
    [Fact]
    public void SavedEmpty_DoesNotRefillVirtualDefaults()
    {
        var rows=new MealPlannerReadRowData(new(){CurrentNutritionPlanId=1,CurrentPlanId=1,CurrentPlanName="Synthetic",Mode="Easy",DefaultMealsPerDay=4},
            [new(1,Date.ToDateTime(TimeOnly.MinValue))],[],[]);
        var day=MealPlanReadModelBuilder.Build(rows,Date,Date).Days[0];
        Assert.Equal("saved",day.Persistence); Assert.Empty(day.Meals); Assert.Equal(0,day.SavedMealCount);
    }
    [Fact]
    public void Nutrition_RoundsPerServingBeforeMultiplication_PropagatesNullPerNutrientAndNegativeRemaining()
    {
        var perServing=MealPlanNutrition.PerServing(100m,10m,0m,null,3);
        var portion=MealPlanNutrition.Multiply(perServing,3);
        Assert.Equal(99.99m,portion.CaloriesKcal); Assert.Equal(9.99m,portion.ProteinG); Assert.Equal(0m,portion.CarbsG); Assert.Null(portion.FatG);
        var totals=MealPlanNutrition.Sum([portion,new(1m,2m,3m,4m)]);
        var remaining=MealPlanNutrition.Remaining(new(90m,12m,5m,6m),totals)!;
        Assert.Equal(-10.99m,remaining.CaloriesKcal); Assert.Null(remaining.FatG);
        Assert.Equal(MealPlanNutrition.Zero,MealPlanNutrition.Sum([]));
        Assert.Equal(0m,MealPlanNutrition.PerServing(0m,0m,0m,0m,1).FatG);
    }
    [Fact]
    public void UnavailableRecipe_UsesOnlyUnavailableWarningAndPreservesRemovableIdentity()
    {
        var item=(PlannerRecipeItemDto)MealPlanReadModelBuilder.Item(new(){Id=8,ItemType="Recipe",RecipeId=3,Quantity=2,SortOrder=4,Available=false});
        Assert.Equal(8,item.Id); Assert.Equal(2,item.Quantity); Assert.Null(item.Name); Assert.Null(item.PerServing);
        Assert.Equal(MealPlanNutrition.Unknown,item.Nutrition); Assert.Equal(["recipe_unavailable"],item.Warnings);
    }
    [Fact]
    public void MixedWarnings_AreDeduplicatedAndManualUnionContainsNoRecipeFields()
    {
        var recipe=new PlannerItemRowData{Id=1,MealId=1,ItemType="Recipe",RecipeId=1,Quantity=1,SortOrder=1,Available=true,RecipeName="Synthetic",Servings=1,CaloriesTotal=0,ProteinTotal=0,CarbsTotal=0,FatTotal=null};
        var manual=new PlannerItemRowData{Id=3,MealId=1,ItemType="Manual",SortOrder=3,ManualDetailId=3,ManualName="Synthetic entry",ManualCaloriesKcal=1,ManualProteinG=2,ManualCarbsG=3,ManualFatG=4};
        var rows=new MealPlannerReadRowData(new(),[new(1,Date.ToDateTime(TimeOnly.MinValue))],[new(1,1,"Synthetic meal",1)],
            [recipe,recipe with {Id=2,SortOrder=2},manual]);
        var day=MealPlanReadModelBuilder.Build(rows,Date,Date).Days[0];
        Assert.Equal(["recipe_fat_unknown"],day.Warnings); Assert.Equal(day.Warnings,day.Meals[0].Warnings);
        Assert.Equal(1m,day.Totals.CaloriesKcal); Assert.Null(day.Totals.FatG);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize<PlannerItemDto>(day.Meals[0].Items[2],new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("manual",json.RootElement.GetProperty("type").GetString());
        Assert.False(json.RootElement.TryGetProperty("recipeId",out _)); Assert.False(json.RootElement.TryGetProperty("quantity",out _));
    }
    [Fact]
    public void MissingManualDetail_FailsInsteadOfFakingZero()
        => Assert.Throws<InternalServerException>(()=>MealPlanReadModelBuilder.Item(new(){Id=1,ItemType="Manual",SortOrder=1}));

    [Theory]
    [InlineData("2026-2-03")][InlineData("0999-12-31")][InlineData("2026-02-30")][InlineData("2026-01-01T00:00:00Z")]
    public void Dates_RejectNonCalendarOrUnsupportedValues(string date)
        => Assert.Throws<ValidationException>(()=>MealPlannerService.ParseDate(date,"from"));

    [Fact]
    public void VirtualKeys_UseIsoCalendarDatesEvenUnderNonGregorianCulture()
    {
        var original=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ar-SA");
            var rows=new MealPlannerReadRowData(new(){CurrentNutritionPlanId=1,CurrentPlanId=1,CurrentPlanName="Synthetic",Mode="Easy",DefaultMealsPerDay=1},[],[],[]);
            Assert.Equal("virtual:2026-10-12:1",MealPlanReadModelBuilder.Build(rows,Date,Date).Days[0].Meals[0].Key);
        }
        finally {CultureInfo.CurrentCulture=original;}
    }
}
