using Appetee.Application.Dtos.MealPlans;

namespace Appetee.Application.Models.MealPlans;

public static class MealPlanNutrition
{
    public static NutrientsDto Zero => new(0m,0m,0m,0m);
    public static NutrientsDto Unknown => new(null,null,null,null);
    public static NutrientsDto PerServing(decimal calories,decimal protein,decimal carbs,decimal? fat,int servings)
    {
        if(servings<=0)throw new ArgumentOutOfRangeException(nameof(servings));
        decimal? Round(decimal? total) => total is {} value ? Math.Round(value/servings,2,MidpointRounding.AwayFromZero) : null;
        return new(Round(calories),Round(protein),Round(carbs),Round(fat));
    }
    public static NutrientsDto Multiply(NutrientsDto value,int quantity) => new(value.CaloriesKcal*quantity,value.ProteinG*quantity,value.CarbsG*quantity,value.FatG*quantity);
    public static NutrientsDto Sum(IEnumerable<NutrientsDto> values)
    {
        var result=Zero;
        foreach(var value in values) result=new(result.CaloriesKcal+value.CaloriesKcal,result.ProteinG+value.ProteinG,result.CarbsG+value.CarbsG,result.FatG+value.FatG);
        return result;
    }
    public static NutrientsDto? Remaining(NutrientsDto? targets,NutrientsDto planned) => targets is null ? null :
        new(targets.CaloriesKcal-planned.CaloriesKcal,targets.ProteinG-planned.ProteinG,targets.CarbsG-planned.CarbsG,targets.FatG-planned.FatG);
}
