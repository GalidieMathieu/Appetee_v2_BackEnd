using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B11ManualItemTests(AppeteeWebApplicationFactory factory) : MealItemTestBase(factory)
{
    [Fact]
    public async Task SparseManualAddAndCompleteUpdate_ExactTotalsPlainTextIdentityAndSameValueNoOp()
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await Current(client,4);
            using var add=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {defaultSlot=3},manualItem=Manual("  Cafe\u0301 <b>food</b>  ")},"1");
            Assert.Equal(HttpStatusCode.Created,add.StatusCode);Assert.Equal(Range,add.Headers.Location!.ToString());
            var receipt=await Receipt(add);var meal=Assert.Single(receipt.Days[0].Meals);Assert.Equal("Afternoon Snack",meal.Name);
            Assert.NotNull(receipt.CreatedMealId);var id=receipt.CreatedItemId!.Value;
            var item=Assert.IsType<PlannerManualItemDto>(Assert.Single(meal.Items));Assert.Equal("Café <b>food</b>",item.Name);
            Assert.Equal(100.01m,item.Nutrition.CaloriesKcal);Assert.Equal(1.02m,item.Nutrition.ProteinG);Assert.Equal(3.04m,item.Nutrition.FatG);
            using var update=await Send(client,HttpMethod.Put,$"/api/meal-plan/manual-items/{id}",Manual("Edited",20000m,5000m,5000m,.01m),"2");
            var updated=await Receipt(update);Assert.Equal("3",updated.Revision);Assert.Null(updated.CreatedMealId);Assert.Null(updated.CreatedItemId);
            var edited=Assert.IsType<PlannerManualItemDto>(updated.Days[0].Meals[0].Items[0]);Assert.Equal(id,edited.Id);Assert.Equal(item.SortOrder,edited.SortOrder);
            Assert.Equal(20000m,updated.Days[0].Totals.CaloriesKcal);Assert.Equal(.01m,updated.Days[0].Totals.FatG);
            var before=await Snapshot(user.userId);
            using var same=await Send(client,HttpMethod.Put,$"/api/meal-plan/manual-items/{id}",Manual(" Edited ",20000m,5000m,5000m,.01m),"3");
            Assert.Equal("3",(await Receipt(same)).Revision);Assert.Equal(before,await Snapshot(user.userId));
            var times=await Factory.Database.QueryAsync<DateTime>("""
                SELECT i.updated_at FROM meal_plan_meal_items i WHERE i.id=@Id
                UNION ALL SELECT m.updated_at FROM meal_plan_day_meals m JOIN meal_plan_meal_items i ON i.meal_plan_day_meal_id=m.id WHERE i.id=@Id
                UNION ALL SELECT d.updated_at FROM meal_plan_days d JOIN meal_plan_day_meals m ON m.meal_plan_day_id=d.id JOIN meal_plan_meal_items i ON i.meal_plan_day_meal_id=m.id WHERE i.id=@Id;
                """,new{Id=id});Assert.Equal(3,times.Count);Assert.Single(times.Distinct());
            using var json=JsonDocument.Parse(JsonSerializer.Serialize<PlannerItemDto>(edited,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Assert.False(json.RootElement.TryGetProperty("quantity",out _));Assert.False(json.RootElement.TryGetProperty("recipeId",out _));
        }
    }

    [Theory]
    [InlineData("caloriesKcal",20000.01)][InlineData("proteinG",5000.01)]
    [InlineData("carbsG",5000.01)][InlineData("fatG",5000.01)]
    public async Task EveryMissingNullNonPositiveExcessAndPrecisionField_HasExactPathAndNoChanges(string field,double excess)
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var mealId=await Meal(client);
            using var add=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual()},"1");var id=(await Receipt(add)).CreatedItemId!.Value;
            var before=await Snapshot(user.userId);
            foreach(var value in new object?[]{null,0m,-1m,(decimal)excess,.001m,"1","NaN",false})
            {
                var manual=new Dictionary<string,object?>{{"name","Synthetic"},{"caloriesKcal",1m},{"proteinG",1m},{"carbsG",1m},{"fatG",1m}};
                manual[field]=value;
                await Invalid(manual);
            }
            var missing=new Dictionary<string,object?>{{"name","Synthetic"},{"caloriesKcal",1m},{"proteinG",1m},{"carbsG",1m},{"fatG",1m}};
            missing.Remove(field);await Invalid(missing);
            async Task Invalid(object manual)
            {
                using var create=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=manual},"2");
                await Field(create,"manualItem."+field);
                using var update=await Send(client,HttpMethod.Put,$"/api/meal-plan/manual-items/{id}",manual,"2");await Field(update,field);
                Assert.Equal(before,await Snapshot(user.userId));
            }
        }
    }

    [Fact]
    public async Task NameBoundsMissingNamesControlsAndOverposting_AreRejectedBeforeSparsePersistence()
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            foreach(var name in new[]{null,""," ","food\nname","food\u0000name","food\u2028name",new string('a',121)})
            {using var response=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {defaultSlot=1},manualItem=Manual(name!)},"0");await Field(response,"manualItem.name");Assert.Equal("",await Snapshot(user.userId));}
            using var missing=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {defaultSlot=1},manualItem=new {caloriesKcal=1,proteinG=1,carbsG=1,fatG=1}},"0");await Field(missing,"manualItem.name");
            foreach(var extra in new[]{"quantity","recipeId","itemType","ownerId","sortOrder"})
            {
                var manual=new Dictionary<string,object>{{"name","Food"},{"caloriesKcal",1},{"proteinG",1},{"carbsG",1},{"fatG",1},{extra,1}};
                using var response=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {defaultSlot=1},manualItem=manual},"0");
                Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await Code(response,"validation_failed");Assert.Equal("",await Snapshot(user.userId));
            }
            var mealId=await Meal(client);using var maximum=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual(string.Concat(Enumerable.Repeat("😀",120)),.01m,.01m,.01m,.01m)},"1");
            var result=await Receipt(maximum);Assert.Equal(.01m,result.Days[0].Totals.CaloriesKcal);
        }
    }

    [Fact]
    public async Task ManualDetailInsertionFailure_RollsBackItemSelectedParentsAndSettings()
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await Current(client);var before=await Snapshot(user.userId);
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b11_fault BEFORE INSERT ON meal_plan_manual_items FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic detail fault';");
            try
            {
                using var response=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {defaultSlot=2},manualItem=Manual()},"1");
                Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);Assert.DoesNotContain("synthetic",await response.Content.ReadAsStringAsync());
                Assert.Equal(before,await Snapshot(user.userId));Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items;"));
            }
            finally{await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b11_fault;");}
        }
    }

    [Fact]
    public async Task WrongSubtypeForeignAndMissingManualUpdate_Uniform404AndNoSideEffects()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        var(other,foreign)=await CreateAuthenticatedClientAsync();
        using(client)using(other)
        {
            var mealId=await Meal(client);var foreignMeal=await Meal(other);
            using var recipe=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=1},"1");var recipeId=(await Receipt(recipe)).CreatedItemId!.Value;
            using var manual=await Send(other,HttpMethod.Post,ManualRoute,new {meal=new {mealId=foreignMeal},manualItem=Manual()},"1");var foreignId=(await Receipt(manual)).CreatedItemId!.Value;
            var before=await Snapshot(user.userId);var foreignBefore=await Snapshot(foreign.userId);
            foreach(var id in new[]{recipeId,foreignId,int.MaxValue})
            {using var response=await Send(client,HttpMethod.Put,$"/api/meal-plan/manual-items/{id}",Manual(),"0");Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);await Code(response,"resource_not_found");}
            using var foreignAdd=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId=foreignMeal},manualItem=Manual()},"0");Assert.Equal(HttpStatusCode.NotFound,foreignAdd.StatusCode);
            using var wrongDate=await Send(client,HttpMethod.Post,"/api/meal-plan/days/2026-10-14/manual-items",new {meal=new {mealId},manualItem=Manual()},"2");Assert.Equal(HttpStatusCode.NotFound,wrongDate.StatusCode);
            using var remove=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{foreignId}",null,"0");Assert.Equal(HttpStatusCode.NotFound,remove.StatusCode);
            Assert.Equal(before,await Snapshot(user.userId));Assert.Equal(foreignBefore,await Snapshot(foreign.userId));
        }
    }

    [Fact]
    public async Task MixedRemoval_CascadesDetailCompactsItemsAndRetainsLastMealAndDay()
    {
        var(client,_)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            var mealId=await Meal(client);
            using var first=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual()},"1");var firstId=(await Receipt(first)).CreatedItemId!.Value;
            using var recipe=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=1},"2");var recipeId=(await Receipt(recipe)).CreatedItemId!.Value;
            using var second=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual("Second")},"3");var secondId=(await Receipt(second)).CreatedItemId!.Value;
            using var remove=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{firstId}",null,"4");var receipt=await Receipt(remove);
            Assert.Equal(new[]{1,2},receipt.Days[0].Meals[0].Items.Select(i=>i.SortOrder));Assert.Equal(new[]{recipeId,secondId},receipt.Days[0].Meals[0].Items.Select(i=>i.Id));
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items WHERE meal_plan_meal_item_id=@Id;",new{Id=firstId}));
            using var removeSecond=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{secondId}",null,"5");await Receipt(removeSecond);
            using var removeRecipe=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{recipeId}",null,"6");var empty=await Receipt(removeRecipe);
            Assert.Equal("saved",empty.Days[0].Persistence);Assert.Equal(mealId,Assert.Single(empty.Days[0].Meals).Id);Assert.Empty(empty.Days[0].Meals[0].Items);Assert.Equal(0m,empty.Days[0].Totals.ProteinG);
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items;"));
        }
    }

    [Fact]
    public async Task MixedCapacityAndConcurrentLastSlot_EnforceTwentyAtomically()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            var mealId=await Meal(client);
            for(var index=1;index<=19;index++)
            {using var add=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual("Food "+index)},index.ToString());await Receipt(add);}
            var responses=await Task.WhenAll(Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual("Last manual")},"20"),Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=1},"20"));
            try{Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);await Code(responses.Single(r=>r.StatusCode==HttpStatusCode.Conflict),"meal_plan_revision_conflict");}
            finally{foreach(var response in responses)response.Dispose();}
            var before=await Snapshot(user.userId);
            using var exceeded=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual("Overflow")},"21");
            Assert.Equal(HttpStatusCode.Conflict,exceeded.StatusCode);await Code(exceeded,"item_limit_reached");Assert.Equal(before,await Snapshot(user.userId));
            Assert.Equal(20,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_meal_items WHERE meal_plan_day_meal_id=@Id;",new{Id=mealId}));
            var range=(await client.GetFromJsonAsync<MealPlannerRangeDto>(Range))!;Assert.Equal(Enumerable.Range(1,20),range.Days[0].Meals[0].Items.Select(i=>i.SortOrder));
        }
    }

    [Fact]
    public async Task CapturedReadbackFailure_RollsBackItemUpdateAndRevision()
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var mealId=await Meal(client);using var add=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual()},"1");var id=(await Receipt(add)).CreatedItemId!.Value;
            await Factory.Database.ExecuteAsync("INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,sort_order,created_at,updated_at) VALUES(@MealId,'Manual',2,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));",new{MealId=mealId});
            var before=await Snapshot(user.userId);
            using var update=await Send(client,HttpMethod.Put,$"/api/meal-plan/manual-items/{id}",Manual("Must roll back"),"2");Assert.Equal(HttpStatusCode.InternalServerError,update.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task OpenApi_ExactTargetsStrictManualBoundsAndOnlyRequestedItemRoutes()
    {
        using var response=await Client.GetAsync("/swagger/v1/swagger.json");response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var paths=json.RootElement.GetProperty("paths");
        foreach(var route in new[]{"/api/meal-plan/days/{date}/recipe-items","/api/meal-plan/days/{date}/manual-items"})
        {var post=paths.GetProperty(route).GetProperty("post");Assert.True(post.GetProperty("responses").TryGetProperty("201",out _));Assert.Contains(post.GetProperty("parameters").EnumerateArray(),p=>p.GetProperty("name").GetString()=="X-Meal-Plan-Revision" && p.GetProperty("required").GetBoolean());}
        Assert.True(paths.GetProperty("/api/meal-plan/items/{itemId}").TryGetProperty("delete",out _));Assert.True(paths.TryGetProperty("/api/meal-plan/copy-week",out _)); // B14 added.
        var schemas=json.RootElement.GetProperty("components").GetProperty("schemas");var target=schemas.GetProperty("MealTargetRequest");Assert.Equal(2,target.GetProperty("oneOf").GetArrayLength());
        Assert.All(target.GetProperty("oneOf").EnumerateArray(),s=>{Assert.Single(s.GetProperty("required").EnumerateArray());Assert.False(s.GetProperty("additionalProperties").GetBoolean());});
        var manual=schemas.GetProperty("ManualItemRequest");Assert.Equal(5,manual.GetProperty("required").GetArrayLength());Assert.False(manual.GetProperty("additionalProperties").GetBoolean());
        foreach(var field in new[]{"caloriesKcal","proteinG","carbsG","fatG"})
        {var value=manual.GetProperty("properties").GetProperty(field);Assert.Equal(.01m,value.GetProperty("minimum").GetDecimal());Assert.Equal(.01m,value.GetProperty("multipleOf").GetDecimal());Assert.False(value.TryGetProperty("nullable",out var nullable) && nullable.GetBoolean());}
    }

    private static async Task Field(HttpResponseMessage response,string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await Code(response,"validation_failed");
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field,out _),await response.Content.ReadAsStringAsync());
    }
}
