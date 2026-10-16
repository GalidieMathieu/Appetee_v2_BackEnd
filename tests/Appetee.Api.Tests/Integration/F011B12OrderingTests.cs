using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using System.Net;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B12OrderingTests(AppeteeWebApplicationFactory factory):PlannerCopyTestBase(factory)
{
    [Fact]
    public async Task DenseMealAndMixedItemPermutations_NoOpEmptyAndRemoveCompaction()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);
            var original=(await Read(client,Source))!.Days[0];var ids=original.Meals.Select(m=>m.Id!.Value).Reverse().ToArray();
            using var reorder=await Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=ids},"0");
            var receipt=await Receipt(reorder);Assert.Equal("1",receipt.Revision);Assert.Equal(ids,receipt.Days[0].Meals.Select(m=>m.Id!.Value));Assert.Equal(new[]{1,2,3},receipt.Days[0].Meals.Select(m=>m.SortOrder));
            var meal=original.Meals[0];var itemIds=meal.Items.Select(i=>i.Id).Reverse().ToArray();
            using var items=await Send(client,HttpMethod.Put,$"/api/meal-plan/meals/{meal.Id}/item-order",new {orderedIds=itemIds},"1");
            var reordered=await Receipt(items);Assert.Equal("2",reordered.Revision);var mixed=reordered.Days[0].Meals.Single(m=>m.Id==meal.Id);
            Assert.Equal(itemIds,mixed.Items.Select(i=>i.Id));Assert.IsType<PlannerManualItemDto>(mixed.Items[0]);
            Assert.Equal(original.Totals,reordered.Days[0].Totals);
            var before=await Snapshot(user.userId);
            using var same=await Send(client,HttpMethod.Put,$"/api/meal-plan/meals/{meal.Id}/item-order",new {orderedIds=itemIds},"2");Assert.Equal("2",(await Receipt(same)).Revision);Assert.Equal(before,await Snapshot(user.userId));
            using var empty=await Send(client,HttpMethod.Put,$"/api/meal-plan/meals/{ids[0]}/item-order",new {orderedIds=Array.Empty<int>()},"2");Assert.Equal("2",(await Receipt(empty)).Revision);Assert.Equal(before,await Snapshot(user.userId));
            using var remove=await Send(client,HttpMethod.Delete,$"/api/meal-plan/items/{itemIds[0]}",null,"2");await Receipt(remove);
            using var compact=await Send(client,HttpMethod.Put,$"/api/meal-plan/meals/{meal.Id}/item-order",new {orderedIds=new[]{itemIds[1]}},"3");var final=await Receipt(compact);Assert.Equal("3",final.Revision);Assert.Equal(1,final.Days[0].Meals.Single(m=>m.Id==meal.Id).Items[0].SortOrder);
        }
    }

    [Fact]
    public async Task SavedEmptyParentAcceptsEmpty_AbsentDayIs404_AndForeignParentIs404BeforeRevision()
    {
        var(client,user)=await CreateAuthenticatedClientAsync();var(other,foreign)=await CreateAuthenticatedClientAsync();
        using(client)using(other)
        {
            await SeedDay(user.userId,Source,0);await SeedDay(foreign.userId,Destination,1,false);
            var foreignMeal=(await Read(other,Destination))!.Days[0].Meals[0].Id;
            var before=await Snapshot(user.userId);var foreignBefore=await Snapshot(foreign.userId);
            using var empty=await Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=Array.Empty<int>()},"0");Assert.Equal("0",(await Receipt(empty)).Revision);
            using var absent=await Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Destination}/meal-order",new {orderedIds=Array.Empty<int>()},"99");Assert.Equal(HttpStatusCode.NotFound,absent.StatusCode);
            foreach(var id in new[]{foreignMeal,int.MaxValue})
            {using var response=await Send(client,HttpMethod.Put,$"/api/meal-plan/meals/{id}/item-order",new {orderedIds=Array.Empty<int>()},"99");Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);await Code(response,"resource_not_found");}
            Assert.Equal(before,await Snapshot(user.userId));Assert.Equal(foreignBefore,await Snapshot(foreign.userId));
        }
    }

    [Fact]
    public async Task IncompleteDuplicateForeignOtherParentAndUnknownMembership_ChangeNothing()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);var(other,foreign)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)using(other)
        {
            await SeedDay(user.userId,Source);await SeedDay(user.userId,Destination);await SeedDay(foreign.userId,Source);
            var day=(await Read(client,Source))!.Days[0];var elsewhere=(await Read(client,Destination))!.Days[0];var foreignDay=(await Read(other,Source))!.Days[0];
            var own=day.Meals.Select(m=>m.Id!.Value).ToArray();var before=await Snapshot(user.userId);var foreignBefore=await Snapshot(foreign.userId);
            foreach(var invalid in new[]{Array.Empty<int>(),new[]{own[0]},new[]{own[0],own[0],own[2]},new[]{own[0],own[1],int.MaxValue},new[]{own[0],own[1],elsewhere.Meals[0].Id!.Value},new[]{own[0],own[1],foreignDay.Meals[0].Id!.Value}})
            {using var response=await Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=invalid},"0");Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await Code(response,"validation_failed");Assert.Equal(before,await Snapshot(user.userId));}
            var items=day.Meals[0].Items.Select(i=>i.Id).ToArray();
            foreach(var invalid in new[]{Array.Empty<int>(),new[]{items[0]},new[]{items[0],items[0]},new[]{items[0],elsewhere.Meals[0].Items[0].Id},new[]{items[0],foreignDay.Meals[0].Items[0].Id},new[]{items[0],int.MaxValue}})
            {using var response=await Send(client,HttpMethod.Put,$"/api/meal-plan/meals/{day.Meals[0].Id}/item-order",new {orderedIds=invalid},"0");Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Equal(before,await Snapshot(user.userId));}
            Assert.Equal(foreignBefore,await Snapshot(foreign.userId));
        }
    }

    [Theory]
    [InlineData("{}")][InlineData("{\"orderedIds\":null}")][InlineData("{\"orderedIds\":[0]}")]
    [InlineData("{\"orderedIds\":[-1]}")][InlineData("{\"orderedIds\":[1.5]}")][InlineData("{\"orderedIds\":[\"1\"]}")]
    [InlineData("{\"orderedIds\":[],\"ownerId\":1}")]
    public async Task StrictRequests_RejectBeforeAnyPersistence(string json)
    {
        var(client,user)=await CreateAuthenticatedClientAsync();
        using(client)using(var request=new HttpRequestMessage(HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order"){Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json")})
        {request.Headers.Add("X-Meal-Plan-Revision","0");using var response=await client.SendAsync(request);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Equal("",await Snapshot(user.userId));}
    }

    [Fact]
    public async Task ConcurrentReorders_OneSuccessOneStale_AndBatchFaultRestoresAllPositions()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);var ids=(await Read(client,Source))!.Days[0].Meals.Select(m=>m.Id!.Value).ToArray();
            var responses=await Task.WhenAll(Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=ids.Reverse().ToArray()},"0"),Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=new[]{ids[1],ids[2],ids[0]}},"0"));
            try{Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);await Code(responses.Single(r=>r.StatusCode==HttpStatusCode.Conflict),"meal_plan_revision_conflict");}
            finally{foreach(var response in responses)response.Dispose();}
            var before=await Snapshot(user.userId);
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b12_fault BEFORE UPDATE ON meal_plan_day_meals FOR EACH ROW BEGIN IF NEW.sort_order=2 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic order fault'; END IF; END;");
            try{using var failed=await Send(client,HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=ids},"1");Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);Assert.Equal(before,await Snapshot(user.userId));}
            finally{await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b12_fault;");}
        }
    }
}
