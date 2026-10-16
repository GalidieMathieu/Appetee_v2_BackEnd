using Appetee.Application.Dtos.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public interface IMealPlannerService
{
    Task<MealPlannerRangeDto> GetRangeAsync(int currentUserId,string? from,string? through,CancellationToken ct);
}
