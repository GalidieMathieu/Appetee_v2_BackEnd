using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Infrastructure.MealPlans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MySqlConnector;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B10RecipeItemTests(AppeteeWebApplicationFactory factory) : MealItemTestBase(factory)
{
    [Fact]
    public async Task PickerReadsNeverMaterialize_AndAddSavesOnlySelectedDefaultReturningItsID()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await Current(client);var before=await Snapshot(user.userId);
            foreach(var route in new[]{Range,"/api/recipes/meal-plan?search=chicken","/api/recipes/1/preview"})
            {using var read=await client.GetAsync(route);read.EnsureSuccessStatusCode();}
            Assert.Equal(before,await Snapshot(user.userId)); // Leaving/cancelling requires no write endpoint.
            using var add=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=2},recipeId=1,quantity=2},"1");
            Assert.Equal(HttpStatusCode.Created,add.StatusCode);Assert.Equal(Range,add.Headers.Location!.ToString());
            var receipt=await Receipt(add);Assert.Equal("2",receipt.Revision);Assert.NotNull(receipt.CreatedMealId);Assert.NotNull(receipt.CreatedItemId);
            var meal=Assert.Single(Assert.Single(receipt.Days).Meals);Assert.Equal("Lunch",meal.Name);Assert.Equal(receipt.CreatedMealId,meal.Id);
            Assert.Equal(1,meal.SortOrder);Assert.Null(meal.DefaultSlot);Assert.False(meal.IsVirtual);
            var item=Assert.IsType<PlannerRecipeItemDto>(Assert.Single(meal.Items));Assert.Equal(receipt.CreatedItemId,item.Id);Assert.Equal(2,item.Quantity);
            Assert.Equal(item.PerServing!.CaloriesKcal*2,item.Nutrition.CaloriesKcal);
            Assert.Equal(JsonSerializer.Serialize(receipt.Days),JsonSerializer.Serialize((await client.GetFromJsonAsync<MealPlannerRangeDto>(Range))!.Days));
        }
    }

    [Fact]
    public async Task SavedMeal_DuplicateDoesNotIncrement_AllWholeQuantitiesWork_ValidSameValueIsNoOp()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            var mealId=await Meal(client);
            using var add=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=1},"1");
            var added=await Receipt(add);Assert.Null(added.CreatedMealId);var itemId=added.CreatedItemId!.Value;
            var before=await Snapshot(user.userId);
            using var duplicate=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=2},"2");
            Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);await Code(duplicate,"recipe_already_in_meal");Assert.Equal(before,await Snapshot(user.userId));
            for(var quantity=1;quantity<=20;quantity++)
            {
                using var update=await Send(client,HttpMethod.Put,$"/api/meal-plan/recipe-items/{itemId}/quantity",new {quantity},quantity==1 ? "2" : quantity.ToString());
                var receipt=await Receipt(update);Assert.Equal((quantity+1).ToString(),receipt.Revision);
                Assert.Equal(quantity,Assert.IsType<PlannerRecipeItemDto>(receipt.Days[0].Meals[0].Items[0]).Quantity);
                if(quantity==1)Assert.Equal(before,await Snapshot(user.userId));
            }
            before=await Snapshot(user.userId);
            using var stale=await Send(client,HttpMethod.Put,$"/api/meal-plan/recipe-items/{itemId}/quantity",new {quantity=20},"20");
            await Code(stale,"meal_plan_revision_conflict");Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Theory]
    [InlineData("{}")][InlineData("{\"meal\":null,\"recipeId\":1,\"quantity\":1}")]
    [InlineData("{\"meal\":{},\"recipeId\":1,\"quantity\":1}")]
    [InlineData("{\"meal\":{\"mealId\":1,\"defaultSlot\":1},\"recipeId\":1,\"quantity\":1}")]
    [InlineData("{\"meal\":{\"mealId\":null,\"defaultSlot\":1},\"recipeId\":1,\"quantity\":1}")]
    [InlineData("{\"meal\":{\"defaultSlot\":null},\"recipeId\":1,\"quantity\":1}")]
    [InlineData("{\"meal\":{\"defaultSlot\":0},\"recipeId\":1,\"quantity\":1}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1},\"recipeId\":0,\"quantity\":1}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1},\"recipeId\":1,\"quantity\":0}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1},\"recipeId\":1,\"quantity\":21}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1},\"recipeId\":1,\"quantity\":1.5}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1},\"recipeId\":1,\"quantity\":\"1\"}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1},\"recipeId\":1,\"quantity\":1,\"ownerId\":1}")]
    [InlineData("{\"meal\":{\"defaultSlot\":1,\"hasMealId\":false},\"recipeId\":1,\"quantity\":1}")]
    public async Task StrictRecipeAdd_RejectsInvalidTargetsQuantitiesAndOverpostingWithoutPersistence(string json)
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)using(var request=new HttpRequestMessage(HttpMethod.Post,RecipeRoute){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")})
        {
            request.Headers.Add("X-Meal-Plan-Revision","0");using var response=await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await Code(response,"validation_failed");Assert.Equal("",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task OwnershipDateSubtypeAndMissingObjects_Uniform404BeforeRevision_WithoutChanges()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        var(other,foreign)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)using(other)
        {
            var foreignMeal=await Meal(other);var mealId=await Meal(client);
            using var add=await Send(other,HttpMethod.Post,RecipeRoute,new {meal=new {mealId=foreignMeal},recipeId=1,quantity=1},"1");
            var foreignItem=(await Receipt(add)).CreatedItemId!.Value;
            using var manual=await Send(client,HttpMethod.Post,ManualRoute,new {meal=new {mealId},manualItem=Manual()},"1");
            var manualId=(await Receipt(manual)).CreatedItemId!.Value;
            var before=await Snapshot(user.userId);var beforeForeign=await Snapshot(foreign.userId);
            foreach(var target in new[]{foreignMeal,int.MaxValue})
            {using var denied=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId=target},recipeId=1,quantity=1},"0");Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);await Code(denied,"resource_not_found");}
            using var wrongDate=await Send(client,HttpMethod.Post,"/api/meal-plan/days/2026-10-14/recipe-items",new {meal=new {mealId},recipeId=1,quantity=1},"2");
            Assert.Equal(HttpStatusCode.NotFound,wrongDate.StatusCode);
            foreach(var id in new[]{foreignItem,manualId,int.MaxValue})
            {using var denied=await Send(client,HttpMethod.Put,$"/api/meal-plan/recipe-items/{id}/quantity",new {quantity=2},"0");Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);}
            using var remove=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{foreignItem}",null,"0");Assert.Equal(HttpStatusCode.NotFound,remove.StatusCode);
            Assert.Equal(before,await Snapshot(user.userId));Assert.Equal(beforeForeign,await Snapshot(foreign.userId));
        }
    }

    [Fact]
    public async Task CompatibilityIsAuthoritative_UnavailableQuantityIsDenied_ButRemovalRetainsEmptyParents()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await Current(client);using var add=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1},"1");
            var itemId=(await Receipt(add)).CreatedItemId!.Value;
            await Factory.Database.ExecuteAsync("INSERT INTO user_ingredient_restrictions(user_id,ingredient_id) SELECT @Id,ingredient_id FROM recipe_ingredients WHERE recipe_id=1 LIMIT 1;",new{Id=user.userId});
            var before=await Snapshot(user.userId);
            using var quantity=await Send(client,HttpMethod.Put,$"/api/meal-plan/recipe-items/{itemId}/quantity",new {quantity=1},"2");
            Assert.Equal(HttpStatusCode.NotFound,quantity.StatusCode);await Code(quantity,"recipe_unavailable");Assert.Equal(before,await Snapshot(user.userId));
            var range=(await client.GetFromJsonAsync<MealPlannerRangeDto>(Range))!;
            Assert.Equal("unavailable",Assert.IsType<PlannerRecipeItemDto>(range.Days[0].Meals[0].Items[0]).Availability);Assert.Null(range.Days[0].Totals.CaloriesKcal);
            using var remove=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{itemId}",null,"2");
            var removed=await Receipt(remove);Assert.Equal("3",removed.Revision);Assert.Equal("saved",removed.Days[0].Persistence);
            Assert.Empty(Assert.Single(removed.Days[0].Meals).Items);Assert.Equal(0m,removed.Days[0].Totals.FatG);Assert.Empty(removed.Days[0].Warnings);
            before=await Snapshot(user.userId);
            using var deniedAdd=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId=removed.Days[0].Meals[0].Id},recipeId=1,quantity=1},"3");
            await Code(deniedAdd,"recipe_unavailable");Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task CanonicalNutritionIsLive_FatUnknownAndKnownZeroDistinct_ReferencedDeleteRestricted()
    {
        var(client,_)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            var mealId=await Meal(client);await Factory.Database.ExecuteAsync("UPDATE recipes SET fat_total=NULL WHERE id=1;");
            using var add=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=2},"1");
            var receipt=await Receipt(add);Assert.Null(receipt.Days[0].Totals.FatG);Assert.Contains("recipe_fat_unknown",receipt.Days[0].Warnings);
            var deletion=await Assert.ThrowsAsync<MySqlException>(()=>Factory.Database.ExecuteAsync("DELETE FROM recipes WHERE id=1;"));
            Assert.Contains("fk_meal_plan_meal_items_recipe",deletion.Message);
            await Factory.Database.ExecuteAsync("UPDATE recipes SET fat_total=0,calories_total=123.45,servings=2 WHERE id=1;");
            var range=(await client.GetFromJsonAsync<MealPlannerRangeDto>(Range))!;
            Assert.Equal("2",range.Revision);Assert.Equal(0m,range.Days[0].Totals.FatG);Assert.Equal(123.46m,range.Days[0].Totals.CaloriesKcal);Assert.Empty(range.Days[0].Warnings);
        }
    }

    [Fact]
    public async Task SparseDefaultValidation_AndFailureAfterParents_RollBackEntireWrite()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            using var noCurrent=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1},"0");
            Assert.Equal(HttpStatusCode.BadRequest,noCurrent.StatusCode);Assert.Equal("",await Snapshot(user.userId));
            await Current(client,2);var before=await Snapshot(user.userId);
            using var wrongSlot=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=3},recipeId=1,quantity=1},"1");
            Assert.Equal(HttpStatusCode.BadRequest,wrongSlot.StatusCode);
            using var missing=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=int.MaxValue,quantity=1},"1");await Code(missing,"recipe_unavailable");
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b10_fault BEFORE INSERT ON meal_plan_meal_items FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic recipe insertion fault';");
            try
            {using var failure=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1},"1");Assert.Equal(HttpStatusCode.InternalServerError,failure.StatusCode);Assert.DoesNotContain("synthetic",await failure.Content.ReadAsStringAsync());Assert.Equal(before,await Snapshot(user.userId));}
            finally{await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b10_fault;");}
            using var success=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1},"1");await Receipt(success);
            before=await Snapshot(user.userId);
            using var savedSlot=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=2},recipeId=1,quantity=1},"2");Assert.Equal(HttpStatusCode.BadRequest,savedSlot.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task ConcurrentFirstAdds_OnlyOneCommitsWithOneRevision()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await Current(client);
            var responses=await Task.WhenAll(Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1},"1"),Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1},"1"));
            try{Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);await Code(responses.Single(r=>r.StatusCode==HttpStatusCode.Conflict),"meal_plan_revision_conflict");}
            finally{foreach(var response in responses)response.Dispose();}
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_meal_items;"));
            Assert.Equal(2L,await Factory.Database.QuerySingleOrDefaultAsync<long>("SELECT revision FROM meal_plan_user_settings WHERE user_id=@Id;",new{Id=user.userId}));
        }
    }

    [Fact]
    public async Task OwnedItemAndRecipeLookups_UseIndexedPlans()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            var mealId=await Meal(client);using var add=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=1},"1");var itemId=(await Receipt(add)).CreatedItemId!.Value;
            foreach(var sql in new[]{MealItemSql.OwnedItem,MealItemSql.LockRecipe,MealItemSql.ItemCount,MealItemSql.ContainsRecipe})
            {
                var plan=await Factory.Database.QueryAsync<ExplainRow>("EXPLAIN "+sql,new {CurrentUserId=user.userId,ItemId=itemId,RecipeId=1,MealId=mealId});
                Assert.NotEmpty(plan);Assert.Contains(plan,r=>r.key is not null);
                // Scalar EXISTS has an outer "No tables used" row with no table/key.
                Assert.All(plan.Where(r=>r.table is not null && !r.table.StartsWith('<')),r=>Assert.NotNull(r.key));
            }
        }
    }

    [Fact]
    public async Task QuantityBoundaryRejectsFractionsNullStringsAndZero_AndCanonicalZerosAreValid()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            var mealId=await Meal(client);
            await Factory.Database.ExecuteAsync("UPDATE recipes SET calories_total=0,protein_total=0,carbs_total=0,fat_total=0 WHERE id=1;");
            using var add=await Send(client,HttpMethod.Post,RecipeRoute,new {meal=new {mealId},recipeId=1,quantity=1},"1");var receipt=await Receipt(add);var id=receipt.CreatedItemId!.Value;
            Assert.Equal(0m,receipt.Days[0].Totals.CaloriesKcal);Assert.Equal(0m,receipt.Days[0].Totals.ProteinG);Assert.Equal(0m,receipt.Days[0].Totals.CarbsG);Assert.Equal(0m,receipt.Days[0].Totals.FatG);
            var before=await Snapshot(user.userId);
            foreach(var json in new[]{"{}","{\"quantity\":null}","{\"quantity\":0}","{\"quantity\":-1}","{\"quantity\":21}","{\"quantity\":1.5}","{\"quantity\":\"1\"}","{\"quantity\":1,\"type\":\"manual\"}"})
            {
                using var request=new HttpRequestMessage(HttpMethod.Put,$"/api/meal-plan/recipe-items/{id}/quantity"){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")};
                request.Headers.Add("X-Meal-Plan-Revision","2");using var rejected=await client.SendAsync(request);Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);await Code(rejected,"validation_failed");Assert.Equal(before,await Snapshot(user.userId));
            }
        }
    }

    [Fact]
    public async Task AllItemRoutes_RequireAuthRevisionCsrfAndExactBoundaries_WithNoWrites()
    {
        var routes=new[]{(HttpMethod.Post,RecipeRoute,(object)new {meal=new {defaultSlot=1},recipeId=1,quantity=1}),
            (HttpMethod.Post,ManualRoute,(object)new {meal=new {defaultSlot=1},manualItem=Manual()}),
            (HttpMethod.Put,"/api/meal-plan/recipe-items/1/quantity",(object)new {quantity=1}),
            (HttpMethod.Put,"/api/meal-plan/manual-items/1",Manual()),(HttpMethod.Delete,"/api/meal-plan/items/1",(object?)null)};
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            foreach(var (method,route,body) in routes)
            {
                using var anonymous=await Send(Client,method,route,body,"0");Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
                using var missing=await Send(client,method,route,body,null);await Code(missing,"revision_required");
                using var malformed=await Send(client,method,route,body,"-1");await Code(malformed,"revision_invalid");
                using var query=await Send(client,method,route+"?task=recipe-picker",body,"0");Assert.Equal(HttpStatusCode.BadRequest,query.StatusCode);
                using var csrf=new HttpRequestMessage(method,route);csrf.Headers.Add("X-Meal-Plan-Revision","0");csrf.Headers.Add("X-CSRF-TOKEN","synthetic-invalid-token");
                if(body is not null)csrf.Content=JsonContent.Create(body);
                using var rejected=await client.SendAsync(csrf);await Code(rejected,"csrf_invalid");
                Assert.Equal("",await Snapshot(user.userId));
            }
            using var deleteBody=await Send(client,HttpMethod.Delete,"/api/meal-plan/items/1",new {quantity=1},"0");Assert.Equal(HttpStatusCode.BadRequest,deleteBody.StatusCode);
            using var media=new HttpRequestMessage(HttpMethod.Post,RecipeRoute){Content=new StringContent("recipeId=1")};media.Headers.Add("X-Meal-Plan-Revision","0");
            using var wrongMedia=await client.SendAsync(media);Assert.Equal(HttpStatusCode.UnsupportedMediaType,wrongMedia.StatusCode);Assert.Equal("",await Snapshot(user.userId));
        }
    }
    private sealed class ExplainRow {public string? key {get;set;} public string? table {get;set;}}
}
