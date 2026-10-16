using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public interface IPlannerCopyService
{
    Task<PlannerMutationDto> CopyDayAsync(int currentUserId,CopyDayRequest request,long expectedRevision,CancellationToken ct);
    Task<PlannerMutationDto> CopyWeekAsync(int currentUserId,CopyWeekRequest request,long expectedRevision,CancellationToken ct);
}
