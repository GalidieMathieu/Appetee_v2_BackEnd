using Appetee.Application.Requests;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Models.MealPlans;

namespace Appetee.Application.Abstractions.Users;

public interface IUserRepository
{
    Task<MealPlanWriteResult<NutritionProfileRowData>> UpdateNutritionProfileAsync(
        int currentUserId, NutritionProfileRequest request, long expectedRevision, CancellationToken ct);

    Task UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct);

}
