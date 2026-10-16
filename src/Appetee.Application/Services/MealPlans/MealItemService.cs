using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Services.MealPlans;

public sealed class MealItemService(IMealItemRepository repository) : IMealItemService
{
    public async Task<PlannerMutationDto> ReorderAsync(int currentUserId,int mealId,ReorderRequest request,long expectedRevision,CancellationToken ct)
    {
        ValidateId(mealId,"mealId");MealPlanRules.ValidateOrder(request.OrderedIds,MealPlanRules.MaxItemsPerMeal);
        return Map(await repository.ReorderAsync(currentUserId,mealId,request.OrderedIds,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> AddRecipeAsync(int currentUserId,string date,AddRecipeItemRequest request,long expectedRevision,CancellationToken ct)
    {
        var parsed=MealPlannerService.ParseDate(date,"date");
        MealPlanRules.ValidateMealTarget(request.Meal);
        ValidateId(request.RecipeId,"recipeId");
        MealPlanRules.ValidateRecipeQuantity(request.Quantity);
        return Map(await repository.AddRecipeAsync(currentUserId,parsed,request.Meal,request.RecipeId,request.Quantity,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> SetRecipeQuantityAsync(int currentUserId,int itemId,SetRecipeQuantityRequest request,long expectedRevision,CancellationToken ct)
    {
        ValidateId(itemId,"itemId");MealPlanRules.ValidateRecipeQuantity(request.Quantity);
        return Map(await repository.SetRecipeQuantityAsync(currentUserId,itemId,request.Quantity,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> AddManualAsync(int currentUserId,string date,AddManualItemRequest request,long expectedRevision,CancellationToken ct)
    {
        var parsed=MealPlannerService.ParseDate(date,"date");MealPlanRules.ValidateMealTarget(request.Meal);
        var manual=MealPlanRules.NormalizeManualItem(request.ManualItem,"manualItem.");
        return Map(await repository.AddManualAsync(currentUserId,parsed,request.Meal,manual,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> UpdateManualAsync(int currentUserId,int itemId,ManualItemRequest request,long expectedRevision,CancellationToken ct)
    {
        ValidateId(itemId,"itemId");var manual=MealPlanRules.NormalizeManualItem(request);
        return Map(await repository.UpdateManualAsync(currentUserId,itemId,manual,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> RemoveAsync(int currentUserId,int itemId,long expectedRevision,CancellationToken ct)
    {
        ValidateId(itemId,"itemId");return Map(await repository.RemoveAsync(currentUserId,itemId,expectedRevision,ct));
    }
    private static void ValidateId(int id,string field)
    {if(id<=0)throw MealPlanRules.InvalidPlanField(field,"Use a positive signed integer ID.");}
    private static PlannerMutationDto Map(MealPlanWriteResult<PlannerMutationRowData> result)
    {
        var captured=result.Payload;
        if(captured.Readback.Context.Revision!=result.Revision)throw new InternalServerException("The captured planner revision is invalid.");
        var range=MealPlanReadModelBuilder.Build(captured.Readback,captured.From,captured.Through);
        return new(range.Revision,range.Settings,range.Days,captured.CreatedMealId,captured.CreatedItemId);
    }
}
