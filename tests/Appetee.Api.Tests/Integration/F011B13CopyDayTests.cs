using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B13CopyDayTests(AppeteeWebApplicationFactory factory):PlannerCopyTestBase(factory)
{
    [Fact]
    public async Task MixedCopy_FreshIdentitiesExactValuesDeepManualAndUnchangedSource()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);var(other,foreign)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)using(other)
        {
            await SeedDay(user.userId,Source);await SeedDay(foreign.userId,Destination);
            var original=(await Read(client,Source))!.Days[0];var originalJson=JsonSerializer.Serialize(original);var foreignBefore=await Snapshot(foreign.userId);
            using var copy=await CopyDay(client,replace:false);Assert.Equal(HttpStatusCode.OK,copy.StatusCode);var receipt=await Receipt(copy);
            Assert.Equal("1",receipt.Revision);Assert.Null(receipt.CreatedMealId);Assert.Null(receipt.CreatedItemId);var day=Assert.Single(receipt.Days);
            Assert.Equal(Shape(original),Shape(day));Assert.NotEqual(original.Id,day.Id);Assert.DoesNotContain(day.Meals.Select(m=>m.Id),id=>original.Meals.Any(m=>m.Id==id));
            Assert.Equal(originalJson,JsonSerializer.Serialize((await Read(client,Source))!.Days[0]));Assert.Equal(foreignBefore,await Snapshot(foreign.userId));
            var manual=day.Meals.SelectMany(m=>m.Items).OfType<PlannerManualItemDto>().Single();
            using var update=await Send(client,HttpMethod.Put,$"/api/meal-plan/manual-items/{manual.Id}",Manual("Copy-only edit"),"1");await Receipt(update);
            Assert.Equal(originalJson,JsonSerializer.Serialize((await Read(client,Source))!.Days[0]));
            Assert.Equal(2,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items x JOIN meal_plan_meal_items i ON i.id=x.meal_plan_meal_item_id JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id;",new {Id=user.userId}));
        }
    }

    [Theory]
    [InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public async Task AbsentAndSavedEmptyAreExact_PersistedDestinationsAlwaysRequireConfirmation(bool sourceSaved,bool destinationSaved)
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await Current(client,4);if(sourceSaved)await SeedDay(user.userId,Source,0);if(destinationSaved)await SeedDay(user.userId,Destination,0);
            var before=await Snapshot(user.userId);
            using var initial=await CopyDay(client,replace:false,revision:"1");
            if(destinationSaved){Assert.Equal(HttpStatusCode.Conflict,initial.StatusCode);await Code(initial,"destination_not_empty");Assert.Equal(before,await Snapshot(user.userId));}
            else initial.EnsureSuccessStatusCode();
            if(!destinationSaved)
            {
                var receipt=await Receipt(initial);Assert.Equal(sourceSaved ? "2" : "1",receipt.Revision);Assert.Equal(sourceSaved ? "saved" : "untouched",receipt.Days[0].Persistence);
                if(!sourceSaved){Assert.Equal(before,await Snapshot(user.userId));Assert.Equal(4,receipt.Days[0].Meals.Count);}else Assert.Empty(receipt.Days[0].Meals);
                return;
            }
            using var confirmed=await CopyDay(client,replace:true,revision:"1");var copied=await Receipt(confirmed);Assert.Equal("2",copied.Revision);
            Assert.Equal(sourceSaved ? "saved" : "untouched",copied.Days[0].Persistence);
            if(sourceSaved)Assert.Empty(copied.Days[0].Meals);else Assert.Equal(4,copied.Days[0].Meals.Count);
        }
    }

    [Fact]
    public async Task SameValuesStillReplaceIdentities_AndAbsentToAbsentCreatesNoSettings()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            using var empty=await CopyDay(client);Assert.Equal("0",(await Receipt(empty)).Revision);Assert.Equal("",await Snapshot(user.userId));
            await SeedDay(user.userId,Source);await SeedDay(user.userId,Destination);var old=(await Read(client,Destination))!.Days[0];
            using var copy=await CopyDay(client);var saved=(await Receipt(copy)).Days[0];Assert.Equal(Shape(old),Shape(saved));Assert.NotEqual(old.Id,saved.Id);
        }
    }

    [Fact]
    public async Task UnavailableSourceOrStaleReviewedContext_Return409WithNoChanges()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);await SeedDay(user.userId,Destination);
            await Factory.Database.ExecuteAsync("INSERT INTO user_diets(user_id,diet_id) VALUES(@Id,1); DELETE FROM diet_recipes WHERE recipe_id=1 AND diet_id=1;",new {Id=user.userId});
            var before=await Snapshot(user.userId);using var unavailable=await CopyDay(client);await Code(unavailable,"source_recipe_unavailable");Assert.Equal(before,await Snapshot(user.userId));
            await Factory.Database.ExecuteAsync("DELETE FROM user_diets WHERE user_id=@Id;",new {Id=user.userId});
            var source=(await Read(client,Source))!.Days[0];using var rename=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{source.Meals[0].Id}",new {name="Changed since review"},"0");await Receipt(rename);
            before=await Snapshot(user.userId);using var stale=await CopyDay(client);await Code(stale,"meal_plan_revision_conflict");Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Theory]
    [InlineData("meal_plan_days")][InlineData("meal_plan_manual_items")]
    public async Task FailureAfterDeleteOrParentInserts_RestoresOriginalDestinationAndRevision(string table)
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);await SeedDay(user.userId,Destination);var before=await Snapshot(user.userId);
            await Factory.Database.ExecuteAsync($"CREATE TRIGGER b13_fault BEFORE INSERT ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic copy fault';");
            try{using var failure=await CopyDay(client);Assert.Equal(HttpStatusCode.InternalServerError,failure.StatusCode);Assert.DoesNotContain("synthetic",await failure.Content.ReadAsStringAsync());Assert.Equal(before,await Snapshot(user.userId));}
            finally{await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b13_fault;");}
        }
    }
}
