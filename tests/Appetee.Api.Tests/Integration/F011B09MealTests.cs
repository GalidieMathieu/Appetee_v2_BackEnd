using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Infrastructure.MealPlans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B09MealTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Date="2026-10-12";
    private const string CreateRoute="/api/meal-plan/days/"+Date+"/meals";
    private const string RangeRoute="/api/meal-plan/days?from="+Date+"&through="+Date;

    [Fact]
    public async Task MaterializeRenamedLunch_SavesOnlySelectedMealAndReturnsAuthoritativeReceipt()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await CreateCurrent(client,4);
            using var response=await Send(client,HttpMethod.Post,CreateRoute,new {name="  Lunch with friends  ",defaultSlot=2},"1");
            Assert.Equal(HttpStatusCode.Created,response.StatusCode);
            Assert.Equal(RangeRoute,response.Headers.Location!.ToString());
            var receipt=(await response.Content.ReadFromJsonAsync<PlannerMutationDto>())!;
            Assert.Equal("2",receipt.Revision); Assert.NotNull(receipt.CreatedMealId); Assert.Null(receipt.CreatedItemId);
            Assert.Equal(4,receipt.Settings.CurrentPlan!.DefaultMealsPerDay);
            var day=Assert.Single(receipt.Days); var meal=Assert.Single(day.Meals);
            Assert.Equal("saved",day.Persistence); Assert.Equal(1,day.SavedMealCount); Assert.Equal("Lunch with friends",meal.Name);
            Assert.Equal(receipt.CreatedMealId,meal.Id);Assert.Equal(1,meal.SortOrder);Assert.False(meal.IsVirtual);Assert.Null(meal.DefaultSlot);
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days WHERE user_id=@Id;",new {Id=user.userId}));
            Assert.Equal(JsonSerializer.Serialize(receipt.Days),JsonSerializer.Serialize((await client.GetFromJsonAsync<MealPlannerRangeDto>(RangeRoute))!.Days));
        }
    }

    [Fact]
    public async Task ExplicitMeals_WorkWithoutCurrent_AppendDensely_AllowDuplicateNames_AndEnforceCap()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            for(var index=0;index<12;index++)
            {
                using var response=await Send(client,HttpMethod.Post,CreateRoute,new {name="New Meal"},index.ToString());
                Assert.Equal(HttpStatusCode.Created,response.StatusCode);
                var receipt=(await response.Content.ReadFromJsonAsync<PlannerMutationDto>())!;
                Assert.Null(receipt.Settings.CurrentPlan);
                Assert.Equal(Enumerable.Range(1,index+1),receipt.Days[0].Meals.Select(m=>m.SortOrder));
            }
            var before=await Snapshot(user.userId);
            using var exceeded=await Send(client,HttpMethod.Post,CreateRoute,new {name="Rejected"},"12");
            Assert.Equal(HttpStatusCode.Conflict,exceeded.StatusCode);
            await AssertCode(exceeded,"meal_limit_reached"); Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Theory]
    [InlineData("{}")][InlineData("{\"name\":null}")][InlineData("{\"name\":\" \"}")]
    [InlineData("{\"name\":\"bad\\nname\"}")][InlineData("{\"name\":\"New Meal\",\"defaultSlot\":null}")]
    [InlineData("{\"name\":\"New Meal\",\"defaultSlot\":0}")][InlineData("{\"name\":\"New Meal\",\"defaultSlot\":9}")]
    [InlineData("{\"name\":\"New Meal\",\"userId\":1}")][InlineData("{\"name\":\"New Meal\",\"sortOrder\":1}")]
    [InlineData("{\"name\":\"New Meal\",\"ownerId\":1}")]
    [InlineData("{\"name\":\"New Meal\",\"defaultSlot\":\"1\"}")]
    [InlineData("{\"name\":\"New Meal\",\"defaultSlot\":1.5}")]
    [InlineData("{\"name\":\"New Meal\",\"hasDefaultSlot\":true}")]
    public async Task StrictCreate_RejectsInvalidFieldsAndOverpostingWithoutAnyRows(string json)
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        using(var request=new HttpRequestMessage(HttpMethod.Post,CreateRoute))
        {
            request.Headers.Add("X-Meal-Plan-Revision","0");request.Content=new StringContent(json,System.Text.Encoding.UTF8,"application/json");
            using var response=await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); await AssertCode(response,"validation_failed");
            Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task NoCurrentOrUnavailableDefaultSlot_CreatesNoParents_StaleCurrentIs409()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            using var noCurrent=await Send(client,HttpMethod.Post,CreateRoute,new {name="Lunch",defaultSlot=1},"0");
            Assert.Equal(HttpStatusCode.BadRequest,noCurrent.StatusCode);Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
            await CreateCurrent(client,2);
            var before=await Snapshot(user.userId);
            using var invalid=await Send(client,HttpMethod.Post,CreateRoute,new {name="Lunch",defaultSlot=3},"1");
            Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
            using var stale=await Send(client,HttpMethod.Post,CreateRoute,new {name="Lunch",defaultSlot=1},"0");
            Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);await AssertCode(stale,"meal_plan_revision_conflict");
            Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task SavedDate_CannotMaterializeAnotherVirtualSlot()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await CreateCurrent(client,4);
            using var created=await Send(client,HttpMethod.Post,CreateRoute,new {name="Lunch",defaultSlot=2},"1");
            created.EnsureSuccessStatusCode(); var before=await Snapshot(user.userId);
            using var invalid=await Send(client,HttpMethod.Post,CreateRoute,new {name="Dinner",defaultSlot=4},"2");
            Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task Rename_NoOpPreservesRevisionAndTimestamps_StaleRenameWritesNothing()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var meal=await CreateSavedMeal(client);
            var stamps=await Timestamps(user.userId);
            using var noop=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{meal}",new {name="  New Meal  "},"1");
            noop.EnsureSuccessStatusCode();Assert.Equal("1",(await noop.Content.ReadFromJsonAsync<PlannerMutationDto>())!.Revision);
            Assert.Equal(stamps,await Timestamps(user.userId));
            using var changed=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{meal}",new {name="Changed"},"1");
            changed.EnsureSuccessStatusCode();Assert.Equal("2",(await changed.Content.ReadFromJsonAsync<PlannerMutationDto>())!.Revision);
            var before=await Snapshot(user.userId);
            using var stale=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{meal}",new {name="Stale"},"1");
            Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
            Assert.Equal("Changed",await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT name FROM meal_plan_day_meals WHERE id=@Id;",new {Id=meal}));
        }
    }

    [Fact]
    public async Task Delete_CascadesBothSubtypes_CompactsOrder_AndKeepsFinalSavedEmptyDay()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var first=await CreateSavedMeal(client);
            using var secondResponse=await Send(client,HttpMethod.Post,CreateRoute,new {name="Second"},"1");
            var second=(await secondResponse.Content.ReadFromJsonAsync<PlannerMutationDto>())!.CreatedMealId!.Value;
            await Factory.Database.ExecuteAsync("""
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
                VALUES(@Meal,'Recipe',1,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),(@Meal,'Manual',NULL,NULL,2,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                INSERT INTO meal_plan_manual_items(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)
                SELECT id,'Synthetic manual',1,1,1,1 FROM meal_plan_meal_items WHERE meal_plan_day_meal_id=@Meal AND item_type='Manual';
                """,new {Meal=first});
            using var delete=await Send(client,HttpMethod.Delete,$"/api/meal-plan/meals/{first}",null,"2");
            delete.EnsureSuccessStatusCode();var receipt=(await delete.Content.ReadFromJsonAsync<PlannerMutationDto>())!;
            Assert.Equal(1,Assert.Single(receipt.Days[0].Meals).SortOrder);
            Assert.Equal(second,receipt.Days[0].Meals[0].Id);
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items;"));
            using var final=await Send(client,HttpMethod.Delete,$"/api/meal-plan/meals/{second}",null,"3");
            final.EnsureSuccessStatusCode();var empty=(await final.Content.ReadFromJsonAsync<PlannerMutationDto>())!.Days[0];
            Assert.Equal("saved",empty.Persistence);Assert.NotNull(empty.Id);Assert.Empty(empty.Meals);
            var reload=(await client.GetFromJsonAsync<MealPlannerRangeDto>(RangeRoute))!.Days[0];
            Assert.Equal("saved",reload.Persistence);Assert.Empty(reload.Meals);
            var before=await Snapshot(user.userId);
            using var repeated=await Send(client,HttpMethod.Delete,$"/api/meal-plan/meals/{second}",null,"4");
            Assert.Equal(HttpStatusCode.NotFound,repeated.StatusCode);Assert.Equal(before,await Snapshot(user.userId));
        }
    }

    [Theory]
    [InlineData("PATCH")][InlineData("DELETE")]
    public async Task ForeignAndMissingMeals_AreUniform404_EvenWithStaleRevision(string method)
    {
        var (client,user)=await CreateAuthenticatedClientAsync();var (other,foreignUser)=await CreateAuthenticatedClientAsync();
        using(client)using(other)
        {
            var foreignMeal=await CreateSavedMeal(other);
            foreach(var id in new[]{foreignMeal,int.MaxValue})
            {
                using var response=await Send(client,new HttpMethod(method),$"/api/meal-plan/meals/{id}",method=="PATCH" ? new {name="Rejected"} : null,"999");
                Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);await AssertCode(response,"resource_not_found");
            }
            Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
            Assert.Equal("1|1|1|0|1",await Snapshot(foreignUser.userId));
        }
    }

    [Fact]
    public async Task ConcurrentFirstCreates_One201One409_OnlyOneDayMealAndRevision()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var responses=await Task.WhenAll(Send(client,HttpMethod.Post,CreateRoute,new {name="First"},"0"),Send(client,HttpMethod.Post,CreateRoute,new {name="Second"},"0"));
            try {Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);}
            finally{foreach(var response in responses)response.Dispose();}
            Assert.Equal("1|1|1|0|1",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task FailureAfterFirstParentInsert_RollsBackSettingsDayAndMeal()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b09_fail_meal BEFORE INSERT ON meal_plan_day_meals FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic meal fault';");
            try
            {
                using var response=await Send(client,HttpMethod.Post,CreateRoute,new {name="Must roll back"},"0");
                Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
                Assert.DoesNotContain("synthetic meal fault",await response.Content.ReadAsStringAsync());
                Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
            }
            finally {await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b09_fail_meal;");}
        }
    }

    [Theory]
    [InlineData(null,"revision_required")][InlineData("-1","revision_invalid")][InlineData("9223372036854775808","revision_invalid")]
    public async Task RequiredRevision_RejectsBeforePersistence(string? revision,string code)
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            using var response=await Send(client,HttpMethod.Post,CreateRoute,new {name="Rejected"},revision);
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);await AssertCode(response,code);
            Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task CsrfInvalidAndUnsupportedContentType_RejectBeforeWrites()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            using var csrf=new HttpRequestMessage(HttpMethod.Post,CreateRoute){Content=JsonContent.Create(new {name="Rejected"})};
            csrf.Headers.Add("X-Meal-Plan-Revision","0");csrf.Headers.Add("X-CSRF-TOKEN","invalid");
            using var rejected=await client.SendAsync(csrf);Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);await AssertCode(rejected,"csrf_invalid");
            using var unsupported=new HttpRequestMessage(HttpMethod.Post,CreateRoute){Content=new StringContent("name=Rejected")};
            unsupported.Headers.Add("X-Meal-Plan-Revision","0");
            using var media=await client.SendAsync(unsupported);Assert.Equal(HttpStatusCode.UnsupportedMediaType,media.StatusCode);
            Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task OwnedMealLookup_UsesIndexedExplainPlan()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var meal=await CreateSavedMeal(client);
            Assert.NotEmpty(await Factory.Database.QueryAsync<object>("EXPLAIN "+MealSql.OwnedMeal,new {CurrentUserId=user.userId,MealId=meal}));
        }
    }

    [Theory]
    [InlineData("POST")][InlineData("PATCH")][InlineData("DELETE")]
    public async Task MealWrites_RequireAuthenticatedSession(string method)
    {
        var route=method=="POST" ? CreateRoute : "/api/meal-plan/meals/1";
        using var response=await Send(Client,new HttpMethod(method),route,method=="DELETE" ? null : new {name="Rejected"},"0");
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days;"));
    }

    [Theory]
    [InlineData("2026-02-30")][InlineData("0999-01-01")][InlineData("2026-1-01")]
    public async Task Create_RejectsMalformedDateWithoutRows(string date)
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            using var response=await Send(client,HttpMethod.Post,$"/api/meal-plan/days/{date}/meals",new {name="Rejected"},"0");
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Equal("0|0|0|0|0",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task CorruptReadback_RollsBackRenameRevisionAndTimestampsBeforeCommit()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var meal=await CreateSavedMeal(client);
            await Factory.Database.ExecuteAsync("""
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,sort_order,created_at,updated_at)
                VALUES(@Meal,'Manual',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                """,new {Meal=meal});
            var before=await Timestamps(user.userId);
            using var response=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{meal}",new {name="Must not commit"},"1");
            Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
            Assert.Equal(before,await Timestamps(user.userId));
            Assert.Equal("New Meal",await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT name FROM meal_plan_day_meals WHERE id=@Id;",new {Id=meal}));
            Assert.Equal("1|1|1|1|1",await Snapshot(user.userId));
        }
    }

    [Fact]
    public async Task Rename_WithMissingSettingsAndSameName_IsTrueNoOp()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var meal=await CreateSavedMeal(client);
            await Factory.Database.ExecuteAsync("DELETE FROM meal_plan_user_settings WHERE user_id=@Id;",new {Id=user.userId});
            using var noop=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{meal}",new {name="New Meal"},"0");
            noop.EnsureSuccessStatusCode();Assert.Equal("0",(await noop.Content.ReadFromJsonAsync<PlannerMutationDto>())!.Revision);
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id=@Id;",new {Id=user.userId}));
        }
    }

    [Fact]
    public async Task OpenApi_DescribesRangeAndMeals_WithoutAddingLaterPhaseEndpoints()
    {
        using var response=await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths=doc.RootElement.GetProperty("paths");
        var range=paths.GetProperty("/api/meal-plan/days").GetProperty("get");
        Assert.All(range.GetProperty("parameters").EnumerateArray(),p=>Assert.True(p.GetProperty("required").GetBoolean()));
        var create=paths.GetProperty("/api/meal-plan/days/{date}/meals").GetProperty("post");
        Assert.True(create.GetProperty("responses").TryGetProperty("201",out _));
        Assert.Contains(create.GetProperty("parameters").EnumerateArray(),p=>p.GetProperty("name").GetString()=="X-Meal-Plan-Revision");
        Assert.True(paths.GetProperty("/api/meal-plan/meals/{mealId}").TryGetProperty("patch",out _));
        Assert.True(paths.TryGetProperty("/api/meal-plan/copy-week",out _)); // B14 now implements this route.
        Assert.True(paths.TryGetProperty("/api/meal-plan/days/{date}/recipe-items",out _)); // Added by B10; B09 contract remains unchanged.
        var schemas=doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(2,schemas.GetProperty("PlannerItemDto").GetProperty("oneOf").GetArrayLength());
        Assert.False(schemas.GetProperty("CreateMealRequest").GetProperty("additionalProperties").GetBoolean());
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client,HttpMethod method,string route,object? body,string? revision)
    {
        using var request=new HttpRequestMessage(method,route);
        if(revision is not null)request.Headers.Add("X-Meal-Plan-Revision",revision);
        if(body is not null)request.Content=JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private static async Task CreateCurrent(HttpClient client,int count)
    {
        using var response=await Send(client,HttpMethod.Post,"/api/nutrition-plans",new {name="Synthetic current",mode="easy",defaultMealsPerDay=count},"0");
        response.EnsureSuccessStatusCode();
    }
    private static async Task<int> CreateSavedMeal(HttpClient client)
    {
        using var response=await Send(client,HttpMethod.Post,CreateRoute,new {name="New Meal"},"0");response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlannerMutationDto>())!.CreatedMealId!.Value;
    }
    private async Task<string?> Snapshot(int userId)=>await Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CONCAT(COALESCE((SELECT revision FROM meal_plan_user_settings WHERE user_id=@Id),0),'|',
        (SELECT COUNT(*) FROM meal_plan_days WHERE user_id=@Id),'|',
        (SELECT COUNT(*) FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id),'|',
        (SELECT COUNT(*) FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id),'|',
        (SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id=@Id));
        """,new {Id=userId});
    private async Task<string?> Timestamps(int userId)=>await Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CONCAT(s.revision,'|',s.updated_at,'|',d.updated_at,'|',m.updated_at)
        FROM meal_plan_user_settings s JOIN meal_plan_days d ON d.user_id=s.user_id JOIN meal_plan_day_meals m ON m.meal_plan_day_id=d.id WHERE s.user_id=@Id;
        """,new {Id=userId});
    private static async Task AssertCode(HttpResponseMessage response,string code)
    {
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code,json.RootElement.GetProperty("code").GetString());Assert.True(json.RootElement.TryGetProperty("traceId",out _));
    }
}
