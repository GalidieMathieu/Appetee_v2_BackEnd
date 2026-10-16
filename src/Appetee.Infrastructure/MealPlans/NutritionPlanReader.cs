using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;
using Dapper;
using System.Data;

namespace Appetee.Infrastructure.MealPlans;

internal static class NutritionPlanReader
{
    internal static async Task<NutritionPlanReadRowData> ReadAsync(IDbConnection connection, IDbTransaction transaction,
        int currentUserId, int? planId, CancellationToken ct)
    {
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            NutritionPlanSql.Context + NutritionPlanSql.Current + (planId.HasValue ? NutritionPlanSql.Detail : NutritionPlanSql.List),
            new { CurrentUserId = currentUserId, PlanId = planId }, transaction,
            commandTimeout: MealPlanRules.SqlCommandTimeoutSeconds, cancellationToken: ct));
        return await MapAsync(grid);
    }

    internal static async Task<NutritionPlanReadRowData> ReadAsync(MealPlanWriteContext context, int currentUserId, int planId)
    {
        using var grid = await context.QueryMultipleAsync(NutritionPlanSql.Context + NutritionPlanSql.Current + NutritionPlanSql.Detail,
            new { CurrentUserId = currentUserId, PlanId = planId });
        return await MapAsync(grid);
    }

    private static async Task<NutritionPlanReadRowData> MapAsync(SqlMapper.GridReader grid)
    {
        var context = await grid.ReadSingleOrDefaultAsync<NutritionPlanContextRowData>()
            ?? throw new UnauthorizedException("Session is no longer valid.");
        var current = await grid.ReadSingleOrDefaultAsync<NutritionPlanRowData>();
        var plans = (await grid.ReadAsync<NutritionPlanRowData>()).AsList();
        return new(context, current, plans);
    }
}
