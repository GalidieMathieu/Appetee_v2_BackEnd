using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Services.MealPlans;

public sealed class MealService(IMealRepository repository) : IMealService
{
    public async Task<PlannerMutationDto> ReorderAsync(int currentUserId,string date,ReorderRequest request,long expectedRevision,CancellationToken ct)
    {
        var parsed=MealPlannerService.ParseDate(date,"date");MealPlanRules.ValidateOrder(request.OrderedIds,MealPlanRules.MaxMealsPerDay);
        return Map(await repository.ReorderAsync(currentUserId,parsed,request.OrderedIds,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> CreateAsync(int currentUserId,string date,CreateMealRequest request,long expectedRevision,CancellationToken ct)
    {
        var parsed=MealPlannerService.ParseDate(date,"date");
        var name=MealPlanRules.NormalizePlanName(request.Name); // Same approved plain-text/NFC/80-scalar bound.
        if(request.HasDefaultSlot && request.DefaultSlot is not (>=1 and <=8))
            throw MealPlanRules.InvalidPlanField("defaultSlot","Use an integer default slot from 1 to 8.");
        return Map(await repository.CreateAsync(currentUserId,parsed,name,request.DefaultSlot,expectedRevision,ct));
    }

    public async Task<PlannerMutationDto> RenameAsync(int currentUserId,int mealId,RenameMealRequest request,long expectedRevision,CancellationToken ct)
    {
        ValidateId(mealId);
        var name=MealPlanRules.NormalizePlanName(request.Name);
        return Map(await repository.RenameAsync(currentUserId,mealId,name,expectedRevision,ct));
    }

    public async Task<PlannerMutationDto> DeleteAsync(int currentUserId,int mealId,long expectedRevision,CancellationToken ct)
    {
        ValidateId(mealId);
        return Map(await repository.DeleteAsync(currentUserId,mealId,expectedRevision,ct));
    }

    private static void ValidateId(int id)
    {
        if(id<=0)throw MealPlanRules.InvalidPlanField("mealId","Use a positive signed integer meal ID.");
    }

    private static PlannerMutationDto Map(MealPlanWriteResult<PlannerMutationRowData> result)
    {
        var captured=result.Payload;
        if(captured.Readback.Context.Revision!=result.Revision)throw new InternalServerException("The captured planner revision is invalid.");
        var range=MealPlanReadModelBuilder.Build(captured.Readback,captured.From,captured.Through);
        return new(range.Revision,range.Settings,range.Days,captured.CreatedMealId,captured.CreatedItemId);
    }
}
