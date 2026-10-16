using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public interface IMealService
{
    Task<PlannerMutationDto> ReorderAsync(int currentUserId,string date,ReorderRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> CreateAsync(int currentUserId,string date,CreateMealRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> RenameAsync(int currentUserId,int mealId,RenameMealRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> DeleteAsync(int currentUserId,int mealId,long expectedRevision,CancellationToken ct);
}
