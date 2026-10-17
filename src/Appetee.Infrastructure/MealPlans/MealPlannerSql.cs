namespace Appetee.Infrastructure.MealPlans;

internal static class MealPlannerSql
{
    internal const string Context="""
        SELECT COALESCE(s.revision,0) Revision,s.current_nutrition_plan_id CurrentNutritionPlanId,
            p.id CurrentPlanId,p.name CurrentPlanName,p.mode Mode,p.default_meals_per_day DefaultMealsPerDay,
            p.target_calories_kcal TargetCaloriesKcal,p.target_protein_g TargetProteinG,
            p.target_carbs_g TargetCarbsG,p.target_fat_g TargetFatG,p.calculation_policy_version CalculationPolicyVersion
        FROM users u LEFT JOIN meal_plan_user_settings s ON s.user_id=u.id
        LEFT JOIN nutrition_plans p ON p.user_id=u.id AND p.id=s.current_nutrition_plan_id
        WHERE u.id=@CurrentUserId;
        """;
    internal const string Days="""
        SELECT d.id Id,d.plan_date PlanDate FROM meal_plan_days d
        WHERE d.user_id=@CurrentUserId AND d.plan_date BETWEEN @From AND @Through ORDER BY d.plan_date;
        """;
    internal const string Meals="""
        SELECT m.id Id,m.meal_plan_day_id DayId,m.name Name,m.sort_order SortOrder
        FROM meal_plan_days d JOIN meal_plan_day_meals m ON m.meal_plan_day_id=d.id
        WHERE d.user_id=@CurrentUserId AND d.plan_date BETWEEN @From AND @Through
        ORDER BY d.plan_date,m.sort_order,m.id;
        """;
    internal static readonly string Items=$"""
        SELECT i.id Id,i.meal_plan_day_meal_id MealId,i.item_type ItemType,i.sort_order SortOrder,
            i.recipe_id RecipeId,i.quantity Quantity,
            CASE WHEN r.id IS NOT NULL AND {RecipeCompatibilitySql.Predicate} THEN 1 ELSE 0 END Available,
            r.name RecipeName,r.servings Servings,r.calories_total CaloriesTotal,r.protein_total ProteinTotal,
            r.carbs_total CarbsTotal,r.fat_total FatTotal,
            manual_item.meal_plan_meal_item_id ManualDetailId,manual_item.name ManualName,
            manual_item.calories_kcal ManualCaloriesKcal,manual_item.protein_g ManualProteinG,
            manual_item.carbs_g ManualCarbsG,manual_item.fat_g ManualFatG
        FROM meal_plan_days d JOIN meal_plan_day_meals m ON m.meal_plan_day_id=d.id
        JOIN meal_plan_meal_items i ON i.meal_plan_day_meal_id=m.id
        LEFT JOIN recipes r ON r.id=i.recipe_id
        LEFT JOIN meal_plan_manual_items AS manual_item ON manual_item.meal_plan_meal_item_id=i.id
        WHERE d.user_id=@CurrentUserId AND d.plan_date BETWEEN @From AND @Through
        ORDER BY d.plan_date,m.sort_order,m.id,i.sort_order,i.id;
        """;
    internal static readonly string Range=Context+Days+Meals+Items;
}
