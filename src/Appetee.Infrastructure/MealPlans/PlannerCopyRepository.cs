using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Services.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Infrastructure.MealPlans;

internal sealed class PlannerCopyRepository(MealPlanTransaction transactions) : IPlannerCopyRepository
{
    public Task<MealPlanWriteResult<PlannerMutationRowData>> CopyDayAsync(int currentUserId,DateOnly source,DateOnly destination,bool replaceExisting,long expectedRevision,CancellationToken ct)=>
        Copy(currentUserId,source,destination,1,replaceExisting,expectedRevision,ct);
    public Task<MealPlanWriteResult<PlannerMutationRowData>> CopyWeekAsync(int currentUserId,DateOnly source,DateOnly destination,bool replaceExisting,long expectedRevision,CancellationToken ct)=>
        Copy(currentUserId,source,destination,7,replaceExisting,expectedRevision,ct);

    private Task<MealPlanWriteResult<PlannerMutationRowData>> Copy(int userId,DateOnly source,DateOnly destination,int count,bool replace,long revision,CancellationToken ct)
    {
        var through=destination.AddDays(count-1);var parameters=MealPlannerReader.Parameters(userId,destination,through);
        return transactions.ExecuteAsync<PlannerMutationRowData>(count==1 ? "copy.day" : "copy.week",userId,revision,async context=>
        {
            var rows=await MealPlannerReader.ReadAsync(context,userId,source,source.AddDays(count-1));
            var recipeIds=rows.Items.Where(i=>i.ItemType=="Recipe" && i.RecipeId.HasValue).Select(i=>i.RecipeId!.Value).Distinct().Order().ToArray();
            if(recipeIds.Length>0)
            {
                var locked=await context.QueryAsync<int>(PlannerCopySql.LockRecipes,new {RecipeIds=recipeIds});
                var compatible=await context.QueryAsync<int>(PlannerCopySql.CompatibleRecipes,new {CurrentUserId=userId,RecipeIds=recipeIds});
                if(locked.Count!=recipeIds.Length || compatible.Count!=recipeIds.Length)
                    throw new ConflictException("A source recipe is not available.",code:"source_recipe_unavailable");
                // Availability may have changed while waiting for canonical authoring locks.
                rows=rows with {Items=rows.Items.Select(i=>i.ItemType=="Recipe" ? i with {Available=true} : i).ToArray()};
            }
            var snapshot=PlannerCopyBuilder.Build(rows,source,destination,count);
            var existing=await context.QueryAsync<PlannerDayRowData>(MealPlannerSql.Days,parameters);
            if(existing.Count>0 && !replace)throw new ConflictException("Confirm replacement of saved destination dates.",code:"destination_not_empty");
            if(existing.Count==0 && snapshot.All(d=>!d.IsSaved))return MealPlanMutation<PlannerMutationRowData>.UnchangedResult(null!);

            // Bound packet use conservatively for utf8mb4 names and parameter overhead, even on small servers.
            var packet=await context.QuerySingleAsync<long>("SELECT @@max_allowed_packet;");
            var batchSize=(int)Math.Clamp(packet/4096,1,PlannerCopySql.MaximumBatchRows);
            await context.ExecuteAsync(PlannerCopySql.DeleteDestination,parameters);
            await Insert(context,"meal_plan_days",["user_id","plan_date","created_at","updated_at"],
                snapshot.Where(d=>d.IsSaved).Select(d=>new object?[]{userId,d.Date.ToDateTime(TimeOnly.MinValue),context.TimestampUtc,context.TimestampUtc}),batchSize);
            var days=await context.QueryAsync<PlannerDayRowData>(MealPlannerSql.Days,parameters);
            var dayIds=days.ToDictionary(d=>DateOnly.FromDateTime(d.PlanDate),d=>d.Id);
            var meals=snapshot.Where(d=>d.IsSaved).SelectMany(d=>d.Meals.Select(m=>(DayId:dayIds[d.Date],Meal:m))).ToArray();
            await Insert(context,"meal_plan_day_meals",["meal_plan_day_id","name","sort_order","created_at","updated_at"],
                meals.Select(m=>new object?[]{m.DayId,m.Meal.Name,m.Meal.SortOrder,context.TimestampUtc,context.TimestampUtc}),batchSize);
            var mealRows=await context.QueryAsync<PlannerMealRowData>(MealPlannerSql.Meals,parameters);
            var mealIds=mealRows.ToDictionary(m=>(m.DayId,m.SortOrder),m=>m.Id);
            var items=meals.SelectMany(m=>m.Meal.Items.Select(i=>(MealId:mealIds[(m.DayId,m.Meal.SortOrder)],Item:i))).ToArray();
            await Insert(context,"meal_plan_meal_items",["meal_plan_day_meal_id","item_type","recipe_id","quantity","sort_order","created_at","updated_at"],
                items.Select(i=>new object?[]{i.MealId,i.Item.ItemType,i.Item.RecipeId,i.Item.Quantity,i.Item.SortOrder,context.TimestampUtc,context.TimestampUtc}),batchSize);
            var itemRows=await context.QueryAsync<ItemMappingRow>(PlannerCopySql.ItemMapping,parameters);
            var itemIds=itemRows.ToDictionary(i=>(i.MealId,i.SortOrder),i=>i.Id);
            await Insert(context,"meal_plan_manual_items",["meal_plan_meal_item_id","name","calories_kcal","protein_g","carbs_g","fat_g"],
                items.Where(i=>i.Item.Manual is not null).Select(i=>new object?[]{itemIds[(i.MealId,i.Item.SortOrder)],
                    i.Item.Manual!.Name,i.Item.Manual.CaloriesKcal,i.Item.Manual.ProteinG,i.Item.Manual.CarbsG,i.Item.Manual.FatG}),batchSize);
            return MealPlanMutation<PlannerMutationRowData>.ChangedResult(null!);
        },ct,isCopy:true,readback:async(context,resultRevision)=>
        {
            var captured=await MealPlannerReader.ReadAsync(context,userId,destination,through);
            if(captured.Context.Revision!=resultRevision)throw new InternalServerException("The captured planner revision is invalid.");
            _=MealPlanReadModelBuilder.Build(captured,destination,through);
            return new(captured,destination,through,null,null);
        });
    }
    private static async Task Insert(MealPlanWriteContext context,string table,string[] columns,IEnumerable<object?[]> rows,int batchSize)
    {
        foreach(var batch in PlannerCopySql.Insert(table,columns,rows,batchSize))await context.ExecuteAsync(batch.Sql,batch.Parameters);
    }
    private sealed record ItemMappingRow(int Id,int MealId,int SortOrder)
    {public ItemMappingRow():this(0,0,0){} }
}
