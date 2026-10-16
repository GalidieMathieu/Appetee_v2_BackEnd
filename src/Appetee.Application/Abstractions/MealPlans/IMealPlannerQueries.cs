using Appetee.Application.RowData.MealPlans;

namespace Appetee.Application.Abstractions.MealPlans;

public interface IMealPlannerQueries
{
    Task<MealPlannerReadRowData> GetRangeAsync(int currentUserId,DateOnly from,DateOnly through,CancellationToken ct);
}
