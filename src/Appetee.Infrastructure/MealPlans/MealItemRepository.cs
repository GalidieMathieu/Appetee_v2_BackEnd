using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Services.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Infrastructure.MealPlans;

internal sealed class MealItemRepository(MealPlanTransaction transactions) : IMealItemRepository
{
    public Task<MealPlanWriteResult<PlannerMutationRowData>> ReorderAsync(int currentUserId,int mealId,IReadOnlyList<int> orderedIds,long expectedRevision,CancellationToken ct)
    {
        OwnedMealRow source=null!;
        return transactions.ExecuteAsync<PlannerMutationRowData>("item.reorder",currentUserId,expectedRevision,async context=>
        {
            var current=await context.QueryAsync<int>(MealItemSql.OrderedIds,new {MealId=mealId});
            MealPlanRules.ValidateOrderMembership(orderedIds,current);
            if(orderedIds.SequenceEqual(current))return MealPlanMutation<PlannerMutationRowData>.UnchangedResult(null!);
            var batch=MealItemSql.Compact(mealId,orderedIds,context.TimestampUtc);await context.ExecuteAsync(batch.Sql,batch.Parameters);
            await TouchParents(context,mealId,source.DayId);
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>source=await context.QuerySingleOrDefaultAsync<OwnedMealRow>(MealSql.OwnedMeal,
            new {CurrentUserId=currentUserId,MealId=mealId}) ?? throw new ItemUnavailableException(),
            readback:async(context,revision)=>await Readback(context,currentUserId,DateOnly.FromDateTime(source.PlanDate),revision));
    }
    public Task<MealPlanWriteResult<PlannerMutationRowData>> AddRecipeAsync(int currentUserId,DateOnly date,MealTargetRequest target,int recipeId,int quantity,long expectedRevision,CancellationToken ct) =>
        Add("item.recipe.add",currentUserId,date,target,recipeId,quantity,null,expectedRevision,ct);
    public Task<MealPlanWriteResult<PlannerMutationRowData>> AddManualAsync(int currentUserId,DateOnly date,MealTargetRequest target,ManualItemRequest manual,long expectedRevision,CancellationToken ct) =>
        Add("item.manual.add",currentUserId,date,target,null,null,manual,expectedRevision,ct);

    private Task<MealPlanWriteResult<PlannerMutationRowData>> Add(string operation,int userId,DateOnly date,MealTargetRequest target,
        int? recipeId,int? quantity,ManualItemRequest? manual,long revision,CancellationToken ct)
    {
        OwnedMealRow? owned=null;int? createdMealId=null,createdItemId=null;
        return transactions.ExecuteAsync<PlannerMutationRowData>(operation,userId,revision,async context=>
        {
            createdMealId=null;createdItemId=null;
            if(recipeId.HasValue)await RequireCompatible(context,userId,recipeId.Value);
            var meal=owned;
            if(meal is null)
            {
                var dayId=await context.QuerySingleOrDefaultAsync<int?>(MealSql.OwnedDay,new {CurrentUserId=userId,Date=date.ToDateTime(TimeOnly.MinValue)});
                var settings=await context.QuerySingleAsync<PlannerContextRowData>(MealPlannerSql.Context,new {CurrentUserId=userId});
                if(dayId.HasValue || settings.CurrentNutritionPlanId is null || settings.DefaultMealsPerDay is null || target.DefaultSlot>settings.DefaultMealsPerDay)
                    throw MealPlanRules.InvalidPlanField("meal.defaultSlot","A current default slot is valid only on an untouched date.");
                dayId=await context.QuerySingleAsync<int>(MealSql.InsertDay,new {CurrentUserId=userId,Date=date.ToDateTime(TimeOnly.MinValue),context.TimestampUtc});
                createdMealId=await context.QuerySingleAsync<int>(MealSql.InsertMeal,new {DayId=dayId.Value,
                    Name=MealDefaults.Names(settings.DefaultMealsPerDay.Value)[target.DefaultSlot!.Value-1],SortOrder=1,context.TimestampUtc});
                meal=new(createdMealId.Value,dayId.Value,date.ToDateTime(TimeOnly.MinValue));
            }
            if(recipeId.HasValue && await context.QuerySingleAsync<bool>(MealItemSql.ContainsRecipe,new {MealId=meal.Id,RecipeId=recipeId.Value}))
                throw new ConflictException("The recipe is already in this meal.",code:"recipe_already_in_meal");
            var count=await context.QuerySingleAsync<int>(MealItemSql.ItemCount,new {MealId=meal.Id});
            if(count>=MealPlanRules.MaxItemsPerMeal)throw new ConflictException("The item limit is reached.",code:"item_limit_reached");
            createdItemId=await context.QuerySingleAsync<int>(MealItemSql.InsertItem,new {MealId=meal.Id,
                ItemType=recipeId.HasValue ? "Recipe" : "Manual",RecipeId=recipeId,Quantity=quantity,SortOrder=count+1,context.TimestampUtc});
            if(manual is not null)await context.ExecuteAsync(MealItemSql.InsertManual,new {ItemId=createdItemId.Value,
                manual.Name,manual.CaloriesKcal,manual.ProteinG,manual.CarbsG,manual.FatG});
            await TouchParents(context,meal.Id,meal.DayId);
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>
        {
            owned=null;
            if(target.MealId.HasValue)
            {
                owned=await context.QuerySingleOrDefaultAsync<OwnedMealRow>(MealSql.OwnedMeal,new {CurrentUserId=userId,MealId=target.MealId.Value});
                if(owned is null || DateOnly.FromDateTime(owned.PlanDate)!=date)throw new ItemUnavailableException();
            }
        },readback:async (context,resultRevision)=>await Readback(context,userId,date,resultRevision,createdMealId,createdItemId));
    }

    public Task<MealPlanWriteResult<PlannerMutationRowData>> SetRecipeQuantityAsync(int currentUserId,int itemId,int quantity,long expectedRevision,CancellationToken ct)
    {
        OwnedItemRow source=null!;
        return transactions.ExecuteAsync<PlannerMutationRowData>("item.recipe.quantity",currentUserId,expectedRevision,async context=>
        {
            await RequireCompatible(context,currentUserId,source.RecipeId!.Value);
            if(source.Quantity==quantity)return MealPlanMutation<PlannerMutationRowData>.UnchangedResult(null!);
            await context.ExecuteAsync(MealItemSql.SetQuantity,new {ItemId=itemId,Quantity=quantity,context.TimestampUtc});
            await TouchParents(context,source.MealId,source.DayId);
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>source=await Owned(context,currentUserId,itemId,"Recipe"),
            readback:async (context,revision)=>await Readback(context,currentUserId,DateOnly.FromDateTime(source.PlanDate),revision));
    }

    public Task<MealPlanWriteResult<PlannerMutationRowData>> UpdateManualAsync(int currentUserId,int itemId,ManualItemRequest manual,long expectedRevision,CancellationToken ct)
    {
        OwnedItemRow source=null!;
        return transactions.ExecuteAsync<PlannerMutationRowData>("item.manual.update",currentUserId,expectedRevision,async context=>
        {
            var previous=await context.QuerySingleOrDefaultAsync<ManualItemRequest>(MealItemSql.ManualDetail,new {ItemId=itemId})
                ?? throw new InternalServerException("Manual item integrity is invalid.");
            if(previous==manual)return MealPlanMutation<PlannerMutationRowData>.UnchangedResult(null!);
            await context.ExecuteAsync(MealItemSql.UpdateManual,new {ItemId=itemId,manual.Name,manual.CaloriesKcal,manual.ProteinG,manual.CarbsG,manual.FatG});
            await context.ExecuteAsync(MealItemSql.TouchItem,new {ItemId=itemId,context.TimestampUtc});
            await TouchParents(context,source.MealId,source.DayId);
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>source=await Owned(context,currentUserId,itemId,"Manual"),
            readback:async (context,revision)=>await Readback(context,currentUserId,DateOnly.FromDateTime(source.PlanDate),revision));
    }

    public Task<MealPlanWriteResult<PlannerMutationRowData>> RemoveAsync(int currentUserId,int itemId,long expectedRevision,CancellationToken ct)
    {
        OwnedItemRow source=null!;
        return transactions.ExecuteAsync<PlannerMutationRowData>("item.remove",currentUserId,expectedRevision,async context=>
        {
            await context.ExecuteAsync(MealItemSql.DeleteItem,new {ItemId=itemId,source.MealId});
            var remaining=await context.QueryAsync<int>(MealItemSql.OrderedIds,new {source.MealId});
            if(remaining.Count>0)
            {var batch=MealItemSql.Compact(source.MealId,remaining,context.TimestampUtc);await context.ExecuteAsync(batch.Sql,batch.Parameters);}
            await TouchParents(context,source.MealId,source.DayId);
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>source=await Owned(context,currentUserId,itemId,null),
            readback:async (context,revision)=>await Readback(context,currentUserId,DateOnly.FromDateTime(source.PlanDate),revision));
    }

    private static async Task RequireCompatible(MealPlanWriteContext context,int userId,int recipeId)
    {
        if(await context.QuerySingleOrDefaultAsync<int?>(MealItemSql.LockRecipe,new {RecipeId=recipeId}) is null
            || !await context.QuerySingleAsync<bool>(MealItemSql.CompatibleRecipe,new {CurrentUserId=userId,RecipeId=recipeId}))
            throw new RecipeUnavailableException();
    }
    private static async Task<OwnedItemRow> Owned(MealPlanWriteContext context,int userId,int itemId,string? type)
    {
        var item=await context.QuerySingleOrDefaultAsync<OwnedItemRow>(MealItemSql.OwnedItem,new {CurrentUserId=userId,ItemId=itemId});
        if(item is null || type is not null && item.ItemType!=type)throw new ItemUnavailableException();
        return item;
    }
    private static async Task TouchParents(MealPlanWriteContext context,int mealId,int dayId)
    {
        await context.ExecuteAsync(MealItemSql.TouchMeal,new {MealId=mealId,DayId=dayId,context.TimestampUtc});
        await context.ExecuteAsync(MealSql.TouchDay,new {DayId=dayId,context.TimestampUtc});
    }
    private static async Task<PlannerMutationRowData> Readback(MealPlanWriteContext context,int userId,DateOnly date,long revision,int? mealId=null,int? itemId=null)
    {
        var rows=await MealPlannerReader.ReadAsync(context,userId,date,date);
        if(rows.Context.Revision!=revision)throw new InternalServerException("The captured planner revision is invalid.");
        _=MealPlanReadModelBuilder.Build(rows,date,date);
        return new(rows,date,date,mealId,itemId);
    }
    private sealed record OwnedMealRow(int Id,int DayId,DateTime PlanDate,string Name="");
    private sealed record OwnedItemRow(int Id,int MealId,int DayId,DateTime PlanDate,string ItemType,int? RecipeId,int? Quantity);
    private sealed class ItemUnavailableException() : ApiException(404,"The item or meal is not available.",code:"resource_not_found");
    private sealed class RecipeUnavailableException() : ApiException(404,"The recipe is not available.",code:"recipe_unavailable");
}
