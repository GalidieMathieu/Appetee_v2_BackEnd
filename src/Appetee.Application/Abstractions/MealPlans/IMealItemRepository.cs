using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;

namespace Appetee.Application.Abstractions.MealPlans;

public interface IMealItemRepository
{
    Task<MealPlanWriteResult<PlannerMutationRowData>> ReorderAsync(int currentUserId,int mealId,IReadOnlyList<int> orderedIds,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> AddRecipeAsync(int currentUserId,DateOnly date,MealTargetRequest target,int recipeId,int quantity,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> SetRecipeQuantityAsync(int currentUserId,int itemId,int quantity,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> AddManualAsync(int currentUserId,DateOnly date,MealTargetRequest target,ManualItemRequest manual,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> UpdateManualAsync(int currentUserId,int itemId,ManualItemRequest manual,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> RemoveAsync(int currentUserId,int itemId,long expectedRevision,CancellationToken ct);
}
