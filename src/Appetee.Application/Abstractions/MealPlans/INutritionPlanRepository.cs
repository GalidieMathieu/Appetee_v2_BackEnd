using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;

namespace Appetee.Application.Abstractions.MealPlans;

public interface INutritionPlanRepository
{
    Task<MealPlanWriteResult<NutritionPlanMutationRowData>> UpdateAsync(int currentUserId, int planId,
        NutritionPlanUpdateData data, long expectedRevision, CancellationToken ct);
    Task<MealPlanWriteResult<NutritionPlanMutationRowData>> DuplicateAsync(int currentUserId, int planId,
        long expectedRevision, CancellationToken ct);
    Task<MealPlanWriteResult<NutritionPlanMutationRowData>> DeleteAsync(int currentUserId, int planId,
        long expectedRevision, CancellationToken ct);
    Task<MealPlanWriteResult<NutritionPlanMutationRowData>> SetCurrentAsync(int currentUserId, int planId,
        long expectedRevision, CancellationToken ct);
    Task<MealPlanWriteResult<NutritionPlanCreatedRowData>> CreateAsync(
        int currentUserId, NutritionPlanCreateData data, long expectedRevision, CancellationToken ct);
}
