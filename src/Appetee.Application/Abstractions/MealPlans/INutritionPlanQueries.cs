using Appetee.Application.RowData.MealPlans;

namespace Appetee.Application.Abstractions.MealPlans;

public interface INutritionPlanQueries
{
    Task<NutritionPlanReadRowData> ListAsync(int currentUserId, CancellationToken ct);
    Task<NutritionPlanReadRowData> GetAsync(int currentUserId, int planId, CancellationToken ct);
}
