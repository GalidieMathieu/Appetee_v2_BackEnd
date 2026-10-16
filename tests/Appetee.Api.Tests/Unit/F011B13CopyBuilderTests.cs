using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B13CopyBuilderTests
{
    [Fact]
    public void AbsentSourceDoesNotCopyCurrentVirtualDefaults_AndSavedEmptyRemainsDistinct()
    {
        var from=new DateOnly(2026,10,12);var destination=from.AddDays(7);
        var context=new PlannerContextRowData{CurrentNutritionPlanId=1,CurrentPlanId=1,DefaultMealsPerDay=8};
        var rows=new MealPlannerReadRowData(context,[new(10,from.AddDays(1).ToDateTime(TimeOnly.MinValue))],[],[]);
        var copy=PlannerCopyBuilder.Build(rows,from,destination,7);
        Assert.Equal(7,copy.Count);Assert.False(copy[0].IsSaved);Assert.Empty(copy[0].Meals);Assert.True(copy[1].IsSaved);Assert.Empty(copy[1].Meals);
        Assert.All(copy,d=>Assert.Empty(d.Meals));
    }

    [Fact]
    public void UnavailableSourceIsConflictAndCorruptManualOrNonDenseOrderIsIntegrityError()
    {
        var date=new DateOnly(2026,10,12);
        var rows=new MealPlannerReadRowData(new(),[new(10,date.ToDateTime(TimeOnly.MinValue))],[new(20,10,"Meal",1)],
            [new(){Id=30,MealId=20,SortOrder=1,ItemType="Recipe",RecipeId=1,Quantity=2,Available=false}]);
        var unavailable=Assert.Throws<ConflictException>(()=>PlannerCopyBuilder.Build(rows,date,date.AddDays(1),1));Assert.Equal("source_recipe_unavailable",unavailable.Code);
        var corrupt=rows with{Items=[new(){Id=30,MealId=20,SortOrder=1,ItemType="Manual"}]};
        Assert.Throws<InternalServerException>(()=>PlannerCopyBuilder.Build(corrupt,date,date.AddDays(1),1));
        Assert.Throws<InternalServerException>(()=>PlannerCopyBuilder.Build(rows with{Meals=[new(20,10,"Meal",2)]},date,date.AddDays(1),1));
    }
}
