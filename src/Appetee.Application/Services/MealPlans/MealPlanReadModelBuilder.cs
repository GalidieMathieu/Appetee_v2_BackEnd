using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;
using System.Globalization;

namespace Appetee.Application.Services.MealPlans;

public static class MealPlanReadModelBuilder
{
    public static MealPlannerRangeDto Build(MealPlannerReadRowData rows,DateOnly from,DateOnly through)
    {
        var settings=Settings(rows.Context);
        var days=rows.Days.ToDictionary(d=>DateOnly.FromDateTime(d.PlanDate));
        var meals=rows.Meals.ToLookup(m=>m.DayId);
        var items=rows.Items.ToLookup(i=>i.MealId);
        var projections=new List<PlannerDayDto>();
        for(var number=from.DayNumber;number<=through.DayNumber;number++)
        {
            var date=DateOnly.FromDayNumber(number);
            var saved=days.GetValueOrDefault(date);
            IReadOnlyList<PlannerMealDto> mapped=saved is null ? Virtual(date,settings.CurrentPlan?.DefaultMealsPerDay)
                : meals[saved.Id].OrderBy(m=>m.SortOrder).ThenBy(m=>m.Id).Select(m=>Meal(m,items[m.Id])).ToArray();
            var totals=MealPlanNutrition.Sum(mapped.Select(m=>m.Totals));
            var targets=settings.CurrentPlan?.Targets;
            projections.Add(new(date,saved?.Id,saved is null ? "untouched" : "saved",saved is null ? 0 : mapped.Count,
                mapped,totals,targets,MealPlanNutrition.Remaining(targets,totals),Warnings(mapped.SelectMany(m=>m.Warnings))));
        }
        return new(rows.Context.Revision.ToString(CultureInfo.InvariantCulture),settings,from,through,projections);
    }

    public static MealPlanSettingsDto Settings(PlannerContextRowData context)
    {
        if(context.CurrentNutritionPlanId is null)return new(null,null);
        if(context.CurrentPlanId!=context.CurrentNutritionPlanId || context.CurrentPlanName is null || context.DefaultMealsPerDay is null)
            throw new InternalServerException("The current nutrition context is invalid.");
        var mode=context.Mode switch { "Easy"=>"easy","Intermediate"=>"intermediate",_=>throw new InternalServerException() };
        var targets=mode=="easy" ? null : new NutrientsDto(context.TargetCaloriesKcal,context.TargetProteinG,context.TargetCarbsG,context.TargetFatG);
        return new(context.CurrentPlanId,new(context.CurrentPlanId.Value,context.CurrentPlanName,mode,context.DefaultMealsPerDay.Value,targets,context.CalculationPolicyVersion));
    }

    private static IReadOnlyList<PlannerMealDto> Virtual(DateOnly date,int? count) => count is null ? [] :
        MealDefaults.Names(count.Value).Select((name,index)=>new PlannerMealDto(null,
            "virtual:"+date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+":"+(index+1).ToString(CultureInfo.InvariantCulture),
            true,index+1,name,index+1,[],MealPlanNutrition.Zero,[])).ToArray();

    private static PlannerMealDto Meal(PlannerMealRowData meal,IEnumerable<PlannerItemRowData> rows)
    {
        var items=rows.OrderBy(i=>i.SortOrder).ThenBy(i=>i.Id).Select(Item).ToArray();
        return new(meal.Id,$"meal:{meal.Id}",false,null,meal.Name,meal.SortOrder,items,MealPlanNutrition.Sum(items.Select(i=>i.Nutrition)),
            Warnings(items.OfType<PlannerRecipeItemDto>().SelectMany(i=>i.Warnings)));
    }

    public static PlannerItemDto Item(PlannerItemRowData row)
    {
        if(row.ItemType=="Manual")
        {
            if(row.ManualDetailId!=row.Id || row.ManualName is null || row.ManualCaloriesKcal is null || row.ManualProteinG is null || row.ManualCarbsG is null || row.ManualFatG is null)
                throw new InternalServerException("Manual item integrity is invalid.");
            return new PlannerManualItemDto(row.Id,row.SortOrder,row.ManualName,new(row.ManualCaloriesKcal,row.ManualProteinG,row.ManualCarbsG,row.ManualFatG));
        }
        if(row.ItemType!="Recipe" || row.RecipeId is null || row.Quantity is null || row.ManualDetailId is not null)
            throw new InternalServerException("Planner item integrity is invalid.");
        if(!row.Available) return new PlannerRecipeItemDto(row.Id,row.SortOrder,row.RecipeId.Value,row.Quantity.Value,"unavailable",null,null,MealPlanNutrition.Unknown,["recipe_unavailable"]);
        if(row.RecipeName is null || row.Servings is not >0 || row.CaloriesTotal is null || row.ProteinTotal is null || row.CarbsTotal is null)
            throw new InternalServerException("Canonical recipe nutrition is invalid.");
        var perServing=MealPlanNutrition.PerServing(row.CaloriesTotal.Value,row.ProteinTotal.Value,row.CarbsTotal.Value,row.FatTotal,row.Servings.Value);
        return new PlannerRecipeItemDto(row.Id,row.SortOrder,row.RecipeId.Value,row.Quantity.Value,"available",row.RecipeName,perServing,
            MealPlanNutrition.Multiply(perServing,row.Quantity.Value),row.FatTotal is null ? ["recipe_fat_unknown"] : []);
    }
    private static IReadOnlyList<string> Warnings(IEnumerable<string> warnings) => warnings.Distinct(StringComparer.Ordinal).ToArray();
}
