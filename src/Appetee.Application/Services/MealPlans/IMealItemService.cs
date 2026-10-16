using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public interface IMealItemService
{
    Task<PlannerMutationDto> ReorderAsync(int currentUserId,int mealId,ReorderRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> AddRecipeAsync(int currentUserId,string date,AddRecipeItemRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> SetRecipeQuantityAsync(int currentUserId,int itemId,SetRecipeQuantityRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> AddManualAsync(int currentUserId,string date,AddManualItemRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> UpdateManualAsync(int currentUserId,int itemId,ManualItemRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> RemoveAsync(int currentUserId,int itemId,long expectedRevision,CancellationToken ct);
}
