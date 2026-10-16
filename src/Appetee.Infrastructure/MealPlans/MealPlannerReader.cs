using System.Data;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;
using Dapper;

namespace Appetee.Infrastructure.MealPlans;

internal static class MealPlannerReader
{
    internal static object Parameters(int userId,DateOnly from,DateOnly through) =>
        new { CurrentUserId=userId,From=from.ToDateTime(TimeOnly.MinValue),Through=through.ToDateTime(TimeOnly.MinValue) };

    internal static async Task<MealPlannerReadRowData> ReadAsync(IDbConnection connection,IDbTransaction transaction,
        int currentUserId,DateOnly from,DateOnly through,CancellationToken ct)
    {
        using var grid=await connection.QueryMultipleAsync(new CommandDefinition(MealPlannerSql.Range,Parameters(currentUserId,from,through),
            transaction,commandTimeout:MealPlanRules.SqlCommandTimeoutSeconds,cancellationToken:ct));
        return await MapAsync(grid);
    }
    internal static async Task<MealPlannerReadRowData> ReadAsync(MealPlanWriteContext context,int userId,DateOnly from,DateOnly through)
    {
        using var grid=await context.QueryMultipleAsync(MealPlannerSql.Range,Parameters(userId,from,through));
        return await MapAsync(grid);
    }
    private static async Task<MealPlannerReadRowData> MapAsync(SqlMapper.GridReader grid)
    {
        var context=await grid.ReadSingleOrDefaultAsync<PlannerContextRowData>() ?? throw new UnauthorizedException();
        var days=(await grid.ReadAsync<PlannerDayRowData>()).AsList();
        var meals=(await grid.ReadAsync<PlannerMealRowData>()).AsList();
        var items=(await grid.ReadAsync<PlannerItemRowData>()).AsList();
        return new(context,days,meals,items);
    }
}
