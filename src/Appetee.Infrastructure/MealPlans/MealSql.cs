using Dapper;
using System.Text;

namespace Appetee.Infrastructure.MealPlans;

internal static class MealSql
{
    internal const string OwnedMeal="""
        SELECT m.id Id,m.meal_plan_day_id DayId,d.plan_date PlanDate,m.name Name
        FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id
        WHERE m.id=@MealId AND d.user_id=@CurrentUserId;
        """;
    internal const string OwnedDay="SELECT id FROM meal_plan_days WHERE user_id=@CurrentUserId AND plan_date=@Date;";
    internal const string MealCount="SELECT COUNT(*) FROM meal_plan_day_meals WHERE meal_plan_day_id=@DayId;";
    internal const string InsertDay="""
        INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at)
        VALUES(@CurrentUserId,@Date,@TimestampUtc,@TimestampUtc); SELECT LAST_INSERT_ID();
        """;
    internal const string InsertMeal="""
        INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at)
        VALUES(@DayId,@Name,@SortOrder,@TimestampUtc,@TimestampUtc); SELECT LAST_INSERT_ID();
        """;
    internal const string RenameMeal="UPDATE meal_plan_day_meals SET name=@Name,updated_at=@TimestampUtc WHERE id=@MealId AND meal_plan_day_id=@DayId;";
    internal const string DeleteMeal="DELETE FROM meal_plan_day_meals WHERE id=@MealId AND meal_plan_day_id=@DayId;";
    internal const string TouchDay="UPDATE meal_plan_days SET updated_at=@TimestampUtc WHERE id=@DayId;";
    internal const string OrderedMealIds="SELECT id FROM meal_plan_day_meals WHERE meal_plan_day_id=@DayId ORDER BY sort_order,id;";

    /// <summary>Persists a validated dense ordering for deletion compaction or full permutation.</summary>
    internal static (string Sql,DynamicParameters Parameters) Compact(int dayId,IReadOnlyList<int> orderedIds,DateTime timestamp)
    {
        var parameters=new DynamicParameters(new { DayId=dayId,TimestampUtc=timestamp,Ids=orderedIds });
        var sql=new StringBuilder("UPDATE meal_plan_day_meals SET sort_order=CASE id ");
        for(var index=0;index<orderedIds.Count;index++)
        {
            sql.Append($"WHEN @Id{index} THEN @Position{index} ");
            parameters.Add($"Id{index}",orderedIds[index]); parameters.Add($"Position{index}",index+1);
        }
        sql.Append("END,updated_at=@TimestampUtc WHERE meal_plan_day_id=@DayId AND id IN @Ids;");
        return (sql.ToString(),parameters);
    }
}
