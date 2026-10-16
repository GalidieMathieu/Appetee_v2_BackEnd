using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.Services.MealPlans;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B15SecurityAndConcurrencyTests(AppeteeWebApplicationFactory factory):PlannerCopyTestBase(factory)
{
    private static IEnumerable<(HttpMethod Method,string Route,object? Body)> Endpoints()
    {
        var profile=new {calculationSex="male",birthYear=1990,heightCm=180,weightKg=80};
        var plan=new {name="Synthetic plan",mode="easy",defaultMealsPerDay=3};
        yield return(HttpMethod.Get,"/api/users/me/nutrition-profile",null);yield return(HttpMethod.Put,"/api/users/me/nutrition-profile",profile);
        yield return(HttpMethod.Get,"/api/nutrition-plans",null);yield return(HttpMethod.Get,"/api/nutrition-plans/1",null);
        yield return(HttpMethod.Post,"/api/nutrition-plans",plan);yield return(HttpMethod.Put,"/api/nutrition-plans/1",new {name="Edited",defaultMealsPerDay=3});
        yield return(HttpMethod.Post,"/api/nutrition-plans/1/duplicate",null);yield return(HttpMethod.Delete,"/api/nutrition-plans/1",null);
        yield return(HttpMethod.Post,"/api/nutrition-plans/calculation-preview",new {profile,goal="maintain",activityLevel="active"});
        yield return(HttpMethod.Put,"/api/meal-plan/current-nutrition-plan",new {nutritionPlanId=1});
        yield return(HttpMethod.Get,Range,null);yield return(HttpMethod.Get,"/api/recipes/meal-plan?search=chicken",null);
        yield return(HttpMethod.Post,$"/api/meal-plan/days/{Source}/meals",new {name="Synthetic meal"});
        yield return(HttpMethod.Patch,"/api/meal-plan/meals/1",new {name="Edited"});yield return(HttpMethod.Delete,"/api/meal-plan/meals/1",null);
        yield return(HttpMethod.Put,$"/api/meal-plan/days/{Source}/meal-order",new {orderedIds=Array.Empty<int>()});
        yield return(HttpMethod.Post,RecipeRoute,new {meal=new {defaultSlot=1},recipeId=1,quantity=1});
        yield return(HttpMethod.Put,"/api/meal-plan/recipe-items/1/quantity",new {quantity=1});
        yield return(HttpMethod.Post,ManualRoute,new {meal=new {defaultSlot=1},manualItem=Manual()});
        yield return(HttpMethod.Put,"/api/meal-plan/manual-items/1",Manual());yield return(HttpMethod.Delete,"/api/meal-plan/items/1",null);
        yield return(HttpMethod.Put,"/api/meal-plan/meals/1/item-order",new {orderedIds=Array.Empty<int>()});
        yield return(HttpMethod.Post,"/api/meal-plan/copy-day",new {sourceDate=Source,destinationDate=Destination,replaceExisting=true});
        yield return(HttpMethod.Post,"/api/meal-plan/copy-week",new {sourceMonday=Source,destinationMonday=Destination,replaceExisting=true});
    }

    [Fact]
    public async Task EveryFeatureRoute_IsAuthenticatedAndAllUnsafeRoutesRejectMissingOrInvalidCsrf()
    {
        foreach(var (method,route,body) in Endpoints())
        {using var response=await Send(Client,method,route,body,"0");Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
        using var raw=Factory.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false,HandleCookies=true});
        using var bootstrap=await raw.GetAsync("/api/auth/csrf");var token=await bootstrap.Content.ReadFromJsonAsync<CsrfBootstrap>();
        using var signup=new HttpRequestMessage(HttpMethod.Post,"/api/auth/sign-up") {Content=JsonContent.Create(new SignUpRequest("release_"+Guid.NewGuid().ToString("N")[..8],Guid.NewGuid().ToString("N")+"@appetee.test","Password123!",[],[]))};
        signup.Headers.Add("Origin","https://localhost:4200");signup.Headers.Add(token!.HeaderName,token.RequestToken);
        using var signed=await raw.SendAsync(signup);signed.EnsureSuccessStatusCode();
        foreach(var (method,route,body) in Endpoints().Where(e=>e.Method!=HttpMethod.Get))
        foreach(var csrf in new string?[]{null,"synthetic-invalid-csrf"})
        {
            using var request=new HttpRequestMessage(method,route);request.Headers.Add("Origin","https://localhost:4200");request.Headers.Add("X-Meal-Plan-Revision","0");
            if(csrf is not null)request.Headers.Add(token.HeaderName,csrf);if(body is not null)request.Content=JsonContent.Create(body);
            using var response=await raw.SendAsync(request);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await Code(response,"csrf_invalid");
        }
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_user_settings;"));
    }

    [Theory]
    [InlineData("/api/meal-plan/copy-day","{}")]
    [InlineData("/api/meal-plan/copy-day","{\"sourceDate\":\"2026-10-12\",\"destinationDate\":\"2026-10-19\"}")]
    [InlineData("/api/meal-plan/copy-day","{\"sourceDate\":\"2026-10-12\",\"destinationDate\":\"2026-10-12\",\"replaceExisting\":true}")]
    [InlineData("/api/meal-plan/copy-day","{\"sourceDate\":\"2026-02-30\",\"destinationDate\":\"2026-10-19\",\"replaceExisting\":true}")]
    [InlineData("/api/meal-plan/copy-day","{\"sourceDate\":\"2026-10-12\",\"destinationDate\":\"2026-10-19\",\"replaceExisting\":\"true\"}")]
    [InlineData("/api/meal-plan/copy-day","{\"sourceDate\":\"2026-10-12\",\"destinationDate\":\"2026-10-19\",\"replaceExisting\":true,\"ownerId\":1}")]
    [InlineData("/api/meal-plan/copy-week","{\"sourceMonday\":\"2026-10-12\",\"destinationMonday\":\"2026-10-19\",\"replaceExisting\":null}")]
    public async Task StrictCopyBoundary_RejectsInvalidBodiesBeforePersistence(string route,string body)
    {
        var(client,user)=await CreateAuthenticatedClientAsync();using(client)
        using(var request=new HttpRequestMessage(HttpMethod.Post,route){Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json")})
        {request.Headers.Add("X-Meal-Plan-Revision","0");using var response=await client.SendAsync(request);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await Code(response,"validation_failed");Assert.Equal("",await Snapshot(user.userId));}
    }

    [Fact]
    public async Task RevisionQueriesAndUnsupportedMedia_AreRejectedWithoutWrites()
    {
        var(client,user)=await CreateAuthenticatedClientAsync();using(client)
        foreach(var (method,route,body) in Endpoints().Where(e=>e.Route.Contains("copy-",StringComparison.Ordinal) || e.Route.EndsWith("-order",StringComparison.Ordinal)))
        {
            using var missing=await Send(client,method,route,body,null);await Code(missing,"revision_required");
            using var malformed=await Send(client,method,route,body,"9223372036854775808");await Code(malformed,"revision_invalid");
            using var query=await Send(client,method,route+"?ownerId=1",body,"0");Assert.Equal(HttpStatusCode.BadRequest,query.StatusCode);
            using var media=new HttpRequestMessage(method,route){Content=new StringContent("synthetic body")};media.Headers.Add("X-Meal-Plan-Revision","0");
            using var badMedia=await client.SendAsync(media);Assert.Equal(HttpStatusCode.UnsupportedMediaType,badMedia.StatusCode);Assert.Equal("",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task CancellationAfterDestinationDeleteAndParentInserts_RestoresWholeWeek()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);using(client)
        {
            await SeedDay(user.userId,Source);await SeedDay(user.userId,Destination);var before=await Snapshot(user.userId);
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b15_pause BEFORE INSERT ON meal_plan_manual_items FOR EACH ROW DO SLEEP(10);");
            using var scope=Factory.Services.CreateScope();using var cancel=new CancellationTokenSource();
            var task=scope.ServiceProvider.GetRequiredService<IPlannerCopyService>().CopyWeekAsync(user.userId,
                new CopyWeekRequest{SourceMonday=Source,DestinationMonday=Destination,ReplaceExisting=true},0,cancel.Token);
            try
            {
                var paused=false;
                for(var attempt=0;attempt<100 && !task.IsCompleted;attempt++)
                {
                    paused=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB=DATABASE() AND STATE='User sleep';")>0;
                    if(paused)break;await Task.Delay(50);
                }
                Assert.True(paused,"The deterministic mid-copy pause was not observed.");cancel.Cancel();
                await Assert.ThrowsAnyAsync<Exception>(async()=>await task);
                Assert.Equal(before,await Snapshot(user.userId));
            }
            finally{cancel.Cancel();try{await task;}catch{}await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b15_pause;");}
        }
    }

    [Fact]
    public async Task LostReplyAfterCommit_IsReconciledByGet_AndOldIntentIsNeverReplayed()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);using(client)
        {
            await SeedDay(user.userId,Source);Factory.MealPlanReplyFault.DropNextCopyReply();
            using var failed=await CopyDay(client);Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);
            var observed=(await Read(client,Destination))!;Assert.Equal("1",observed.Revision);Assert.Equal("saved",observed.Days[0].Persistence);
            Assert.Single(Factory.MealPlanDiagnostics.Operations,o=>o.Operation=="copy.day");
            var before=await Snapshot(user.userId);using var originalIntent=await CopyDay(client);await Code(originalIntent,"meal_plan_revision_conflict");Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task AccountClosure_RemovesOnlyOwnerRootsAndChildren_AndRetainsCanonicalRecipes()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);var(other,foreign)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)using(other)
        {
            await Current(client);await Current(other);await SeedDay(user.userId,Source);await SeedDay(foreign.userId,Source);
            var foreignBefore=await Snapshot(foreign.userId);
            using var closed=await client.DeleteAsync("/api/users/me");closed.EnsureSuccessStatusCode();
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM users WHERE id=@Id;",new {Id=user.userId}));
            Assert.Equal("",await Snapshot(user.userId));Assert.Equal(foreignBefore,await Snapshot(foreign.userId));
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM nutrition_plans WHERE user_id=@Id;",new {Id=user.userId}));
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipes WHERE id=1;"));
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items;"));
        }
    }

    [Fact]
    public async Task AccountClosureRootFailure_RollsBackOutboxSettingsDaysPlansAndUser()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);using(client)
        {
            await Current(client);await SeedDay(user.userId,Source);var before=await Snapshot(user.userId);
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b15_close BEFORE DELETE ON nutrition_plans FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic closure fault';");
            try
            {
                using var closed=await client.DeleteAsync("/api/users/me");Assert.Equal(HttpStatusCode.InternalServerError,closed.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
                Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM users WHERE id=@Id;",new {Id=user.userId}));
                Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_closure_cleanup WHERE former_user_id=@Id;",new {Id=user.userId}));
            }
            finally{await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b15_close;");}
        }
    }

    [Fact]
    public async Task OpenApi_AllFourNewRoutesHaveStrictRequestsRevisionsAndDocumentedSuccess()
    {
        using var response=await Client.GetAsync("/swagger/v1/swagger.json");response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths=json.RootElement.GetProperty("paths");
        foreach(var (route,method) in new[]{("/api/meal-plan/copy-day","post"),("/api/meal-plan/copy-week","post"),("/api/meal-plan/days/{date}/meal-order","put"),("/api/meal-plan/meals/{mealId}/item-order","put")})
        {var operation=paths.GetProperty(route).GetProperty(method);Assert.True(operation.GetProperty("responses").TryGetProperty("200",out _));Assert.Contains(operation.GetProperty("parameters").EnumerateArray(),p=>p.GetProperty("name").GetString()=="X-Meal-Plan-Revision" && p.GetProperty("required").GetBoolean());}
        var schemas=json.RootElement.GetProperty("components").GetProperty("schemas");
        foreach(var name in new[]{"CopyDayRequest","CopyWeekRequest","ReorderRequest"})Assert.False(schemas.GetProperty(name).GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(schemas.GetProperty("CopyDayRequest").GetProperty("required").EnumerateArray(),p=>p.GetString()=="replaceExisting");
        Assert.True(schemas.GetProperty("ReorderRequest").GetProperty("properties").GetProperty("orderedIds").GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(12,paths.GetProperty("/api/meal-plan/days/{date}/meal-order").GetProperty("put").GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("properties").GetProperty("orderedIds").GetProperty("maxItems").GetInt32());
    }
    private sealed record CsrfBootstrap(string RequestToken,string HeaderName);
}
