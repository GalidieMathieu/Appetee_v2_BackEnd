using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public interface INutritionPlanService
{
    Task<NutritionPlanMutationDto> UpdateAsync(int currentUserId, int planId,
        UpdateNutritionPlanRequest request, long expectedRevision, CancellationToken ct);
    Task<NutritionPlanMutationDto> DuplicateAsync(int currentUserId, int planId, long expectedRevision, CancellationToken ct);
    Task<NutritionPlanMutationDto> DeleteAsync(int currentUserId, int planId, long expectedRevision, CancellationToken ct);
    Task<NutritionPlanMutationDto> SetCurrentAsync(int currentUserId, SetCurrentNutritionPlanRequest request,
        long expectedRevision, CancellationToken ct);
    Task<NutritionPlanListDto> ListAsync(int currentUserId, CancellationToken ct);
    Task<NutritionPlanDetailDto> GetAsync(int currentUserId, int planId, CancellationToken ct);
    Task<NutritionPlanMutationDto> CreateAsync(int currentUserId, CreateNutritionPlanRequest request,
        long expectedRevision, CancellationToken ct);
}
