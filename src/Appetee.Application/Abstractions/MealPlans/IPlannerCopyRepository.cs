using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;

namespace Appetee.Application.Abstractions.MealPlans;

public interface IPlannerCopyRepository
{
    Task<MealPlanWriteResult<PlannerMutationRowData>> CopyDayAsync(int currentUserId,DateOnly source,DateOnly destination,bool replaceExisting,long expectedRevision,CancellationToken ct);
    Task<MealPlanWriteResult<PlannerMutationRowData>> CopyWeekAsync(int currentUserId,DateOnly source,DateOnly destination,bool replaceExisting,long expectedRevision,CancellationToken ct);
}
