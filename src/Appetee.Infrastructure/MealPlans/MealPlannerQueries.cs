using System.Data;
using System.Diagnostics;
using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Appetee.Infrastructure.MealPlans;

public sealed class MealPlannerQueries(IDbConnectionFactory db,ILogger<MealPlannerQueries> logger) : IMealPlannerQueries
{
    public async Task<MealPlannerReadRowData> GetRangeAsync(int currentUserId,DateOnly from,DateOnly through,CancellationToken ct)
    {
        var start=Stopwatch.GetTimestamp();
        using var connection=await db.CreateOpenConnectionAsync(ct);
        using var transaction=connection.BeginTransaction(IsolationLevel.RepeatableRead);
        var rows=await MealPlannerReader.ReadAsync(connection,transaction,currentUserId,from,through,ct);
        transaction.Commit();
        logger.LogInformation(Appetee.Application.Diagnostics.LogEvents.PlannerReadCompleted, "Planner range completed. DayRows {DayRows}; MealRows {MealRows}; ItemRows {ItemRows}; ElapsedMs {ElapsedMs}",
            rows.Days.Count,rows.Meals.Count,rows.Items.Count,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        return rows;
    }
}
