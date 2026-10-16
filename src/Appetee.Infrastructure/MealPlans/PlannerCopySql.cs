using Dapper;

namespace Appetee.Infrastructure.MealPlans;

internal static class PlannerCopySql
{
    internal const int MaximumBatchRows=128;
    internal const string LockRecipes="SELECT r.id FROM recipes r WHERE r.id IN @RecipeIds ORDER BY r.id FOR SHARE;";
    internal static readonly string CompatibleRecipes=$"SELECT r.id FROM recipes r WHERE r.id IN @RecipeIds AND {RecipeCompatibilitySql.Predicate} ORDER BY r.id;";
    internal const string DeleteDestination="DELETE FROM meal_plan_days WHERE user_id=@CurrentUserId AND plan_date BETWEEN @From AND @Through;";
    internal const string ItemMapping="""
        SELECT i.id Id,i.meal_plan_day_meal_id MealId,i.sort_order SortOrder
        FROM meal_plan_days d JOIN meal_plan_day_meals m ON m.meal_plan_day_id=d.id
        JOIN meal_plan_meal_items i ON i.meal_plan_day_meal_id=m.id
        WHERE d.user_id=@CurrentUserId AND d.plan_date BETWEEN @From AND @Through;
        """;

    // Tables/columns are Infrastructure constants; every value (including names/dates) is parameterized.
    internal static IEnumerable<(string Sql,DynamicParameters Parameters)> Insert(string table,string[] columns,
        IEnumerable<object?[]> values,int batchSize)
    {
        foreach(var chunk in values.Chunk(batchSize))
        {
            var parameters=new DynamicParameters();var tuples=new List<string>(chunk.Length);
            for(var row=0;row<chunk.Length;row++)
            {
                if(chunk[row].Length!=columns.Length)throw new InvalidOperationException("Invalid copy batch shape.");
                var placeholders=new string[columns.Length];
                for(var column=0;column<columns.Length;column++)
                {var key=$"v{row}_{column}";parameters.Add(key,chunk[row][column]);placeholders[column]="@"+key;}
                tuples.Add("("+string.Join(',',placeholders)+")");
            }
            yield return($"INSERT INTO {table} ({string.Join(',',columns)}) VALUES {string.Join(',',tuples)};",parameters);
        }
    }
}
