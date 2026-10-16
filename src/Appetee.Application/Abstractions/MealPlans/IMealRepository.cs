using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;

namespace Appetee.Application.Abstractions.MealPlans;

public interface IMealRepository
{
    Task<MealPlanWriteResult<PlannerMutationRowData>> ReorderAsync(int currentUserId,DateOnly date,IReadOnlyList<int> orderedIds,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> CreateAsync(int currentUserId,DateOnly date,string name,int? defaultSlot,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> RenameAsync(int currentUserId,int mealId,string name,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> DeleteAsync(int currentUserId,int mealId,long expectedRevision,CancellationToken ct);
}
