using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Models.MealPlans;

public sealed record PlannerCopyManual(string Name,decimal CaloriesKcal,decimal ProteinG,decimal CarbsG,decimal FatG);
public sealed record PlannerCopyItem(string ItemType,int SortOrder,int? RecipeId,int? Quantity,PlannerCopyManual? Manual);
public sealed record PlannerCopyMeal(string Name,int SortOrder,IReadOnlyList<PlannerCopyItem> Items);
public sealed record PlannerCopyDay(DateOnly Date,bool IsSaved,IReadOnlyList<PlannerCopyMeal> Meals);

/// <summary>Captures exact persisted source intent, never virtual defaults or nutrition snapshots.</summary>
public static class PlannerCopyBuilder
{
    public static IReadOnlyList<PlannerCopyDay> Build(MealPlannerReadRowData source,DateOnly from,DateOnly destination,int count)
    {
        if(count is not (1 or 7))throw new ArgumentOutOfRangeException(nameof(count));
        var days=source.Days.ToDictionary(d=>DateOnly.FromDateTime(d.PlanDate));
        var meals=source.Meals.ToLookup(m=>m.DayId);var items=source.Items.ToLookup(i=>i.MealId);
        var result=new List<PlannerCopyDay>(count);
        for(var offset=0;offset<count;offset++)
        {
            if(!days.TryGetValue(from.AddDays(offset),out var day))
            {result.Add(new(destination.AddDays(offset),false,[]));continue;}
            var savedMeals=meals[day.Id].OrderBy(m=>m.SortOrder).ThenBy(m=>m.Id).ToArray();
            Dense(savedMeals.Select(m=>m.SortOrder),MealPlanRules.MaxMealsPerDay);
            var copiedMeals=new List<PlannerCopyMeal>();
            foreach(var meal in savedMeals)
            {
                var savedItems=items[meal.Id].OrderBy(i=>i.SortOrder).ThenBy(i=>i.Id).ToArray();
                Dense(savedItems.Select(i=>i.SortOrder),MealPlanRules.MaxItemsPerMeal);
                var copiedItems=new List<PlannerCopyItem>();
                foreach(var item in savedItems)
                {
                    if(item.ItemType=="Recipe")
                    {
                        if(!item.Available)throw new ConflictException("A source recipe is not available.",code:"source_recipe_unavailable");
                        if(item.RecipeId is not >0 || item.Quantity is not (>=1 and <=20) || item.ManualDetailId is not null)throw Integrity();
                        copiedItems.Add(new("Recipe",item.SortOrder,item.RecipeId,item.Quantity,null));
                    }
                    else if(item.ItemType=="Manual")
                    {
                        if(item.ManualDetailId!=item.Id || item.ManualName is null || item.RecipeId is not null || item.Quantity is not null
                            || item.ManualCaloriesKcal is not (>=.01m and <=20000m) || item.ManualProteinG is not (>=.01m and <=5000m)
                            || item.ManualCarbsG is not (>=.01m and <=5000m) || item.ManualFatG is not (>=.01m and <=5000m))throw Integrity();
                        copiedItems.Add(new("Manual",item.SortOrder,null,null,new(item.ManualName,item.ManualCaloriesKcal.Value,
                            item.ManualProteinG.Value,item.ManualCarbsG.Value,item.ManualFatG.Value)));
                    }
                    else throw Integrity();
                }
                copiedMeals.Add(new(meal.Name,meal.SortOrder,copiedItems));
            }
            result.Add(new(destination.AddDays(offset),true,copiedMeals));
        }
        return result;
    }
    private static void Dense(IEnumerable<int> positions,int maximum)
    {
        var values=positions.ToArray();
        if(values.Length>maximum || !values.SequenceEqual(Enumerable.Range(1,values.Length)))throw Integrity();
    }
    private static InternalServerException Integrity()=>new("The saved source graph is invalid.");
}
