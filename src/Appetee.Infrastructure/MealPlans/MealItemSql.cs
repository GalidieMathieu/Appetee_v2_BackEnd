using Dapper;
using System.Text;

namespace Appetee.Infrastructure.MealPlans;

internal static class MealItemSql
{
    internal const string OwnedItem="""
        SELECT i.id Id,i.meal_plan_day_meal_id MealId,m.meal_plan_day_id DayId,d.plan_date PlanDate,
            i.item_type ItemType,i.recipe_id RecipeId,i.quantity Quantity
        FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id
        JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE i.id=@ItemId AND d.user_id=@CurrentUserId;
        """;
    internal const string LockRecipe="SELECT r.id FROM recipes r WHERE r.id=@RecipeId ORDER BY r.id FOR SHARE;";
    internal static readonly string CompatibleRecipe=$"SELECT EXISTS(SELECT 1 FROM recipes r WHERE r.id=@RecipeId AND {RecipeCompatibilitySql.Predicate});";
    internal const string ItemCount="SELECT COUNT(*) FROM meal_plan_meal_items WHERE meal_plan_day_meal_id=@MealId;";
    internal const string ContainsRecipe="SELECT EXISTS(SELECT 1 FROM meal_plan_meal_items WHERE meal_plan_day_meal_id=@MealId AND recipe_id=@RecipeId);";
    internal const string InsertItem="""
        INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
        VALUES(@MealId,@ItemType,@RecipeId,@Quantity,@SortOrder,@TimestampUtc,@TimestampUtc); SELECT LAST_INSERT_ID();
        """;
    internal const string InsertManual="""
        INSERT INTO meal_plan_manual_items(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)
        VALUES(@ItemId,@Name,@CaloriesKcal,@ProteinG,@CarbsG,@FatG);
        """;
    internal const string ManualDetail="""
        SELECT name Name,calories_kcal CaloriesKcal,protein_g ProteinG,carbs_g CarbsG,fat_g FatG
        FROM meal_plan_manual_items WHERE meal_plan_meal_item_id=@ItemId;
        """;
    internal const string UpdateManual="""
        UPDATE meal_plan_manual_items SET name=@Name,calories_kcal=@CaloriesKcal,protein_g=@ProteinG,carbs_g=@CarbsG,fat_g=@FatG
        WHERE meal_plan_meal_item_id=@ItemId;
        """;
    internal const string SetQuantity="UPDATE meal_plan_meal_items SET quantity=@Quantity,updated_at=@TimestampUtc WHERE id=@ItemId AND item_type='Recipe';";
    internal const string TouchItem="UPDATE meal_plan_meal_items SET updated_at=@TimestampUtc WHERE id=@ItemId;";
    internal const string TouchMeal="UPDATE meal_plan_day_meals SET updated_at=@TimestampUtc WHERE id=@MealId AND meal_plan_day_id=@DayId;";
    internal const string DeleteItem="DELETE FROM meal_plan_meal_items WHERE id=@ItemId AND meal_plan_day_meal_id=@MealId;";
    internal const string OrderedIds="SELECT id FROM meal_plan_meal_items WHERE meal_plan_day_meal_id=@MealId ORDER BY sort_order,id;";
    internal static (string Sql,DynamicParameters Parameters) Compact(int mealId,IReadOnlyList<int> ids,DateTime timestamp)
    {
        var parameters=new DynamicParameters(new {MealId=mealId,Ids=ids,TimestampUtc=timestamp});
        var sql=new StringBuilder("UPDATE meal_plan_meal_items SET sort_order=CASE id ");
        for(var index=0;index<ids.Count;index++)
        {sql.Append($"WHEN @Id{index} THEN @Position{index} ");parameters.Add($"Id{index}",ids[index]);parameters.Add($"Position{index}",index+1);}
        sql.Append("END,updated_at=@TimestampUtc WHERE meal_plan_day_meal_id=@MealId AND id IN @Ids;");
        return(sql.ToString(),parameters);
    }
}
