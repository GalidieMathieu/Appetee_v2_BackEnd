using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;
using Appetee.Application.Services.MealPlans;

namespace Appetee.Infrastructure.MealPlans;

internal sealed class MealRepository(MealPlanTransaction transactions) : IMealRepository
{
    public Task<MealPlanWriteResult<PlannerMutationRowData>> ReorderAsync(int currentUserId,DateOnly date,IReadOnlyList<int> orderedIds,long expectedRevision,CancellationToken ct)
    {
        int dayId=0;
        return transactions.ExecuteAsync<PlannerMutationRowData>("meal.reorder",currentUserId,expectedRevision,async context=>
        {
            var current=await context.QueryAsync<int>(MealSql.OrderedMealIds,new {DayId=dayId});
            MealPlanRules.ValidateOrderMembership(orderedIds,current);
            if(orderedIds.SequenceEqual(current))return MealPlanMutation<PlannerMutationRowData>.UnchangedResult(null!);
            var batch=MealSql.Compact(dayId,orderedIds,context.TimestampUtc);await context.ExecuteAsync(batch.Sql,batch.Parameters);
            await context.ExecuteAsync(MealSql.TouchDay,new {DayId=dayId,context.TimestampUtc});
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>dayId=await context.QuerySingleOrDefaultAsync<int?>(MealSql.OwnedDay,
            new {CurrentUserId=currentUserId,Date=date.ToDateTime(TimeOnly.MinValue)}) ?? throw new MealUnavailableException(),
            readback:async(context,revision)=>await Readback(context,currentUserId,date,null,revision));
    }
    public Task<MealPlanWriteResult<PlannerMutationRowData>> CreateAsync(int currentUserId,DateOnly date,string name,
        int? defaultSlot,long expectedRevision,CancellationToken ct)
    {
        int? createdMealId=null;
        return transactions.ExecuteAsync<PlannerMutationRowData>("meal.create",currentUserId,expectedRevision,async context =>
        {
            createdMealId=null;
            var dayId=await context.QuerySingleOrDefaultAsync<int?>(MealSql.OwnedDay,
                new { CurrentUserId=currentUserId,Date=date.ToDateTime(TimeOnly.MinValue) });
            if(defaultSlot.HasValue)
            {
                var settings=await context.QuerySingleAsync<PlannerContextRowData>(MealPlannerSql.Context,new {CurrentUserId=currentUserId});
                if(dayId.HasValue || settings.CurrentNutritionPlanId is null || settings.DefaultMealsPerDay is null || defaultSlot>settings.DefaultMealsPerDay)
                    throw MealPlanRules.InvalidPlanField("defaultSlot","A current default slot is valid only on an untouched date.");
            }
            if(!dayId.HasValue)
                dayId=await context.QuerySingleAsync<int>(MealSql.InsertDay,
                    new {CurrentUserId=currentUserId,Date=date.ToDateTime(TimeOnly.MinValue),context.TimestampUtc});
            var count=await context.QuerySingleAsync<int>(MealSql.MealCount,new {DayId=dayId.Value});
            if(count>=MealPlanRules.MaxMealsPerDay)throw new ConflictException("The saved-meal limit is reached.",code:"meal_limit_reached");
            createdMealId=await context.QuerySingleAsync<int>(MealSql.InsertMeal,
                new {DayId=dayId.Value,Name=name,SortOrder=count+1,context.TimestampUtc});
            await context.ExecuteAsync(MealSql.TouchDay,new {DayId=dayId.Value,context.TimestampUtc});
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,readback:async (context,revision)=>await Readback(context,currentUserId,date,createdMealId,revision));
    }

    public Task<MealPlanWriteResult<PlannerMutationRowData>> RenameAsync(int currentUserId,int mealId,string name,long expectedRevision,CancellationToken ct)
    {
        OwnedMealRow source=null!;
        return transactions.ExecuteAsync<PlannerMutationRowData>("meal.rename",currentUserId,expectedRevision,async context =>
        {
            if(source.Name==name)return MealPlanMutation<PlannerMutationRowData>.UnchangedResult(null!);
            await context.ExecuteAsync(MealSql.RenameMeal,new {MealId=mealId,source.DayId,Name=name,context.TimestampUtc});
            await context.ExecuteAsync(MealSql.TouchDay,new {source.DayId,context.TimestampUtc});
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>source=await Owned(context,currentUserId,mealId),
            readback:async (context,revision)=>await Readback(context,currentUserId,DateOnly.FromDateTime(source.PlanDate),null,revision));
    }

    public Task<MealPlanWriteResult<PlannerMutationRowData>> DeleteAsync(int currentUserId,int mealId,long expectedRevision,CancellationToken ct)
    {
        OwnedMealRow source=null!;
        return transactions.ExecuteAsync<PlannerMutationRowData>("meal.delete",currentUserId,expectedRevision,async context =>
        {
            await context.ExecuteAsync(MealSql.DeleteMeal,new {MealId=mealId,source.DayId});
            var remaining=await context.QueryAsync<int>(MealSql.OrderedMealIds,new {source.DayId});
            if(remaining.Count>0)
            {
                var batch=MealSql.Compact(source.DayId,remaining,context.TimestampUtc);
                await context.ExecuteAsync(batch.Sql,batch.Parameters);
            }
            await context.ExecuteAsync(MealSql.TouchDay,new {source.DayId,context.TimestampUtc});
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,validateOwnership:async context=>source=await Owned(context,currentUserId,mealId),
            readback:async (context,revision)=>await Readback(context,currentUserId,DateOnly.FromDateTime(source.PlanDate),null,revision));
    }

    private static async Task<OwnedMealRow> Owned(MealPlanWriteContext context,int userId,int mealId) =>
        await context.QuerySingleOrDefaultAsync<OwnedMealRow>(MealSql.OwnedMeal,new {CurrentUserId=userId,MealId=mealId}) ?? throw new MealUnavailableException();
    private static async Task<PlannerMutationRowData> Readback(MealPlanWriteContext context,int userId,DateOnly date,int? createdMealId,long revision)
    {
        var rows=await MealPlannerReader.ReadAsync(context,userId,date,date);
        if(rows.Context.Revision!=revision)throw new InternalServerException("The captured planner revision is invalid.");
        // Validate the complete projection before commit, including manual-detail integrity.
        // A failed mapping must not commit a mutation and then masquerade as a rollback.
        _=MealPlanReadModelBuilder.Build(rows,date,date);
        return new(rows,date,date,createdMealId,null);
    }
    private sealed class MealUnavailableException() : ApiException(404,"The meal is not available.",code:"resource_not_found");
    private sealed record OwnedMealRow(int Id,int DayId,DateTime PlanDate,string Name);
}
