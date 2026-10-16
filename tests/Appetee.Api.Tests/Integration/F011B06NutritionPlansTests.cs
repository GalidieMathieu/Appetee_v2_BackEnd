using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Requests.MealPlans;
using Appetee.Infrastructure.MealPlans;
using Appetee.Application.Services.MealPlans;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B06NutritionPlansTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Path = "/api/nutrition-plans";
    private static object Easy(string name = "Easy Plan", int count = 3) => new { name, mode = "easy", defaultMealsPerDay = count };
    private static object Intermediate(string name = "Athlete Plan", string? policy = null, int? year = null) => new
    {
        name, mode = "intermediate", defaultMealsPerDay = 4,
        calculation = new NutritionCalculationRequest(new("male", DateTime.UtcNow.Year - 28, 180m, 80m), "maintain", "active"),
        reviewedPolicyVersion = policy ?? NutritionPolicy.Version, reviewedCalculationYear = year ?? DateTime.UtcNow.Year,
    };
    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, object body, string? revision = "0")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };
        if (revision is not null) request.Headers.TryAddWithoutValidation("X-Meal-Plan-Revision", revision);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task EmptyList_IsCoherentNullContextAndWritesNothing()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            using var response = await client.GetAsync(Path);
            response.EnsureSuccessStatusCode(); Assert.True(response.Headers.CacheControl!.NoStore);
            var dto = await response.Content.ReadFromJsonAsync<NutritionPlanListDto>();
            Assert.Equal(0, dto!.Revision); Assert.Empty(dto.Plans);
            Assert.Null(dto.Settings.CurrentPlan); Assert.Null(dto.Settings.CurrentNutritionPlanId);
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EasyCreate_HasNullCalculationAndPreservesSavedProfile(bool saved)
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            if (saved) await Factory.Database.ExecuteAsync("""
                UPDATE users SET calculation_sex='female',birth_year=@Year,height_cm=170,weight_kg=65 WHERE id=@Id;
                """, new { Id = auth.userId, Year = DateTime.UtcNow.Year - 30 });
            var before = await ProfileSnapshotAsync(auth.userId);
            using var response = await CreateAsync(client, Easy("  Cafe\u0301  ", 1));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
            var dto = await response.Content.ReadFromJsonAsync<NutritionPlanMutationDto>();
            Assert.Equal(1, dto!.Revision); Assert.Equal("Café", dto.Plan!.Name); Assert.Equal("easy", dto.Plan.Mode);
            Assert.Equal($"{Path}/{dto.Plan.Id}", response.Headers.Location!.OriginalString);
            Assert.Equal(dto.Plan.Id, dto.Settings.CurrentNutritionPlanId); Assert.True(dto.PlannerInvalidated);
            Assert.Null(dto.Profile); Assert.Null(dto.DeletedPlanId); Assert.Null(dto.Plan.Targets);
            Assert.Null(dto.Plan.CalculationInputs); Assert.Null(dto.Plan.CalculationPolicyVersion);
            Assert.Null(dto.Plan.CalculatedAt); Assert.Null(dto.Plan.RmrKcal); Assert.Null(dto.Plan.MaintenanceKcal);
            Assert.Null(dto.Plan.Goal); Assert.Null(dto.Plan.ActivityLevel); Assert.Empty(dto.Plan.Warnings);
            Assert.Equal(DateTimeKind.Utc, dto.Plan.CreatedAt.Kind); Assert.Equal(dto.Plan.CreatedAt, dto.Plan.UpdatedAt);
            Assert.Equal(before, await ProfileSnapshotAsync(auth.userId));
            Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days;"));
            using var get = await client.GetAsync(response.Headers.Location);
            get.EnsureSuccessStatusCode(); var detail = await get.Content.ReadFromJsonAsync<NutritionPlanDetailDto>();
            Assert.False(detail!.ProfileDiffers); Assert.Equal(dto.Plan.Id, detail.Plan.Id);
            var list = await client.GetFromJsonAsync<NutritionPlanListDto>(Path);
            Assert.Single(list!.Plans); Assert.Null(list.Plans[0].Targets);
        }
    }

    [Fact]
    public async Task IntermediateCreate_RecomputesAndCommitsProfilePlanCurrentWithOneRevision()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            using var response = await CreateAsync(client, Intermediate());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<NutritionPlanMutationDto>();
            Assert.Equal(1, dto!.Revision); Assert.True(dto.PlannerInvalidated);
            Assert.Equal("intermediate", dto.Plan!.Mode); Assert.NotNull(dto.Profile);
            Assert.Equal(new NutrientsDto(3500, 128, 506, 107), dto.Plan.Targets);
            Assert.Equal(2005.48m, dto.Plan.RmrKcal); Assert.Equal(3509.60m, dto.Plan.MaintenanceKcal);
            Assert.Equal(NutritionPolicy.Version, dto.Plan.CalculationPolicyVersion);
            Assert.Equal(28, dto.Plan.CalculationInputs!.AgeYears);
            Assert.Equal(dto.Plan.CreatedAt, dto.Plan.CalculatedAt); Assert.Equal(dto.Plan.CreatedAt, dto.Plan.UpdatedAt);
            Assert.Equal(dto.Plan.Id, dto.Settings.CurrentNutritionPlanId);
            var profile = await client.GetFromJsonAsync<NutritionProfileDto>("/api/users/me/nutrition-profile");
            Assert.Equal(1, profile!.Revision); Assert.Equal(dto.Profile, profile.Profile);
            var detail = await client.GetFromJsonAsync<NutritionPlanDetailDto>($"{Path}/{dto.Plan.Id}");
            Assert.False(detail!.ProfileDiffers); Assert.Equal(dto.Plan.Targets, detail.Plan.Targets);
            using var second = await CreateAsync(client, Intermediate("Second"), "1");
            second.EnsureSuccessStatusCode(); var next = await second.Content.ReadFromJsonAsync<NutritionPlanMutationDto>();
            Assert.Equal(2, next!.Revision); Assert.Null(next.Profile); Assert.False(next.PlannerInvalidated);
            Assert.Equal(dto.Plan.Id, next.Settings.CurrentNutritionPlanId);
            Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days;"));
        }
    }

    [Fact]
    public async Task Detail_ProfileDiffersAndWarningsUseStoredInputsWithoutRecalculation()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var body = new { name="Warned",mode="intermediate",defaultMealsPerDay=4,
                calculation=new NutritionCalculationRequest(new("female",DateTime.UtcNow.Year-36,170m,65m),"fatLoss","active"),
                reviewedPolicyVersion=NutritionPolicy.Version,reviewedCalculationYear=DateTime.UtcNow.Year };
            using var created = await CreateAsync(client, body); created.EnsureSuccessStatusCode();
            var receipt = await created.Content.ReadFromJsonAsync<NutritionPlanMutationDto>();
            var original = receipt!.Plan!;
            Assert.Contains(NutritionPolicy.PopulationWarning, original.Warnings);
            using var update = new HttpRequestMessage(HttpMethod.Put,"/api/users/me/nutrition-profile")
            { Content=JsonContent.Create(new NutritionProfileRequest("male",DateTime.UtcNow.Year-30,180m,80m)) };
            update.Headers.Add("X-Meal-Plan-Revision","1");
            using var updated = await client.SendAsync(update); updated.EnsureSuccessStatusCode();
            var before = await SnapshotAsync();
            var detail = await client.GetFromJsonAsync<NutritionPlanDetailDto>($"{Path}/{original.Id}");
            Assert.True(detail!.ProfileDiffers); Assert.Equal(2,detail.Revision);
            Assert.Equal(original.Targets,detail.Plan.Targets); Assert.Equal(original.CalculationInputs,detail.Plan.CalculationInputs);
            Assert.Equal(original.CalculatedAt,detail.Plan.CalculatedAt); Assert.Equal(original.UpdatedAt,detail.Plan.UpdatedAt);
            Assert.Equal(original.Warnings,detail.Plan.Warnings); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task List_IsBoundedOrderedAndSummaryDoesNotSerializeDetailFields()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var ids = new List<int>();
            for(var i=0;i<3;i++)
            {
                using var created = await CreateAsync(client, Easy($"Plan {i}"),i.ToString()); created.EnsureSuccessStatusCode();
                ids.Add((await created.Content.ReadFromJsonAsync<NutritionPlanMutationDto>())!.Plan!.Id);
            }
            await Factory.Database.ExecuteAsync("UPDATE nutrition_plans SET updated_at='2026-10-01' WHERE user_id=@Id;",new {Id=auth.userId});
            var before = await SnapshotAsync();
            using var response = await client.GetAsync(Path); response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(); using var json = JsonDocument.Parse(text);
            var list = JsonSerializer.Deserialize<NutritionPlanListDto>(text,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(new[] {ids[0],ids[2],ids[1]},list.Plans.Select(p=>p.Id));
            Assert.Equal("3",json.RootElement.GetProperty("revision").GetString());
            foreach(var plan in json.RootElement.GetProperty("plans").EnumerateArray())
            {
                Assert.Equal(11,plan.EnumerateObject().Count());
                Assert.False(plan.TryGetProperty("calculationInputs",out _)); Assert.False(plan.TryGetProperty("warnings",out _));
                Assert.False(plan.TryGetProperty("rmrKcal",out _)); Assert.False(plan.TryGetProperty("maintenanceKcal",out _));
                Assert.EndsWith("Z",plan.GetProperty("createdAt").GetString());
            }
            Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task ForeignAndMissingDetail_AreIndistinguishableAndCannotChangeOtherUser()
    {
        var (client, _) = await CreateAuthenticatedClientAsync(); var (other, _) = await CreateAuthenticatedClientAsync();
        using(client) using(other)
        {
            using var created = await CreateAsync(other, Easy()); created.EnsureSuccessStatusCode();
            var id=(await created.Content.ReadFromJsonAsync<NutritionPlanMutationDto>())!.Plan!.Id;
            var before=await SnapshotAsync();
            using var foreign=await client.GetAsync($"{Path}/{id}"); using var missing=await client.GetAsync($"{Path}/2147483647");
            var a=await ErrorAsync(foreign,HttpStatusCode.NotFound,"resource_not_found");
            var b=await ErrorAsync(missing,HttpStatusCode.NotFound,"resource_not_found");
            Assert.Equal(a.GetProperty("detail").GetString(),b.GetProperty("detail").GetString());
            Assert.Empty((await client.GetFromJsonAsync<NutritionPlanListDto>(Path))!.Plans);
            Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("cafe")]
    [InlineData("CAFÉ")]
    [InlineData("Cafe\u0301")]
    public async Task CollationNameConflict_Returns409AndRollsBack(string name)
    {
        var (client, _) = await CreateAuthenticatedClientAsync(); using(client)
        {
            using var created=await CreateAsync(client,Easy("Café")); created.EnsureSuccessStatusCode();
            var before=await SnapshotAsync(); using var duplicate=await CreateAsync(client,Easy(name),"1");
            await ErrorAsync(duplicate,HttpStatusCode.Conflict,"plan_name_conflict"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("name","\"\"")]
    [InlineData("name","\"   \"")]
    [InlineData("name","\"bad\\nname\"")]
    [InlineData("name","\"bad\\tname\"")]
    [InlineData("name","null")]
    [InlineData("name","REMOVE")]
    [InlineData("mode","\"Easy\"")]
    [InlineData("mode","0")]
    [InlineData("mode","null")]
    [InlineData("mode","REMOVE")]
    [InlineData("defaultMealsPerDay","0")]
    [InlineData("defaultMealsPerDay","9")]
    [InlineData("defaultMealsPerDay","3.5")]
    [InlineData("defaultMealsPerDay","\"3\"")]
    [InlineData("defaultMealsPerDay","null")]
    [InlineData("defaultMealsPerDay","REMOVE")]
    [InlineData("calculation","null")]
    [InlineData("reviewedPolicyVersion","null")]
    [InlineData("reviewedCalculationYear","null")]
    [InlineData("reviewedCalculationYear","2026")]
    [InlineData("isCurrent","true")]
    [InlineData("currentNutritionPlanId","1")]
    [InlineData("userId","1")]
    [InlineData("ownerId","1")]
    [InlineData("targets","{}")]
    [InlineData("createdAt","\"2026-10-06\"")]
    [InlineData("revision","\"0\"")]
    [InlineData("hasCalculation","true")]
    public async Task StrictCreate_RejectsInvalidEasyOrOverpostedFieldsWithoutWrites(string field,string value)
    {
        var (client, _) = await CreateAuthenticatedClientAsync(); using(client)
        {
            var node=JsonSerializer.SerializeToNode(Easy())!.AsObject();
            if(value=="REMOVE") node.Remove(field); else node[field]=JsonNode.Parse(value);
            var before=await SnapshotAsync(); using var response=await CreateAsync(client,node);
            var problem=await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed");
            Assert.True(problem.GetProperty("errors").EnumerateObject().Any()); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("calculation","REMOVE")]
    [InlineData("calculation","null")]
    [InlineData("reviewedPolicyVersion","REMOVE")]
    [InlineData("reviewedPolicyVersion","null")]
    [InlineData("reviewedCalculationYear","REMOVE")]
    [InlineData("reviewedCalculationYear","null")]
    [InlineData("reviewedCalculationYear","\"2026\"")]
    public async Task Intermediate_MissingOrMalformedReviewInputsAre400WithoutWrites(string field,string value)
    {
        var (client, _) = await CreateAuthenticatedClientAsync(); using(client)
        {
            var node=JsonSerializer.SerializeToNode(Intermediate())!.AsObject();
            if(value=="REMOVE") node.Remove(field); else node[field]=JsonNode.Parse(value);
            var before=await SnapshotAsync(); using var response=await CreateAsync(client,node);
            await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StalePreviewPolicyOrYear_Returns409AndCreatesNothing(bool policy)
    {
        var (client, _) = await CreateAuthenticatedClientAsync(); using(client)
        {
            var before=await SnapshotAsync(); using var response=await CreateAsync(client,Intermediate(
                policy:policy?"old-policy":NutritionPolicy.Version,year:policy?DateTime.UtcNow.Year:DateTime.UtcNow.Year-1));
            await ErrorAsync(response,HttpStatusCode.Conflict,"calculation_preview_stale"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData(null,"revision_required")]
    [InlineData("-1","revision_invalid")]
    [InlineData("1.0","revision_invalid")]
    [InlineData("1","meal_plan_revision_conflict")]
    public async Task InvalidOrStaleRevision_CannotCreateSettingsOrPlan(string? revision,string code)
    {
        var (client, _) = await CreateAuthenticatedClientAsync(); using(client)
        {
            var before=await SnapshotAsync(); using var response=await CreateAsync(client,Easy(),revision);
            await ErrorAsync(response,code=="meal_plan_revision_conflict"?HttpStatusCode.Conflict:HttpStatusCode.BadRequest,code);
            Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public async Task InjectedFailure_RollsBackProfilePlanCurrentAndProvisionalSettings(bool failRevision,bool existing)
    {
        var (client, auth) = await CreateAuthenticatedClientAsync(); using(client)
        {
            if(existing)
            {
                using var first=await CreateAsync(client,Easy()); first.EnsureSuccessStatusCode();
                await Factory.Database.ExecuteAsync("UPDATE users SET calculation_sex='female',birth_year=@Year,height_cm=170,weight_kg=65 WHERE id=@Id;",
                    new {Id=auth.userId,Year=DateTime.UtcNow.Year-30});
            }
            var before=await SnapshotAsync();
            var trigger=failRevision
                ? "CREATE TRIGGER b06_failure BEFORE UPDATE ON meal_plan_user_settings FOR EACH ROW BEGIN IF NEW.revision > OLD.revision THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic test failure'; END IF; END;"
                : "CREATE TRIGGER b06_failure AFTER INSERT ON nutrition_plans FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic test failure';";
            await Factory.Database.ExecuteAsync(trigger);
            try
            {
                using var response=await CreateAsync(client,Intermediate(),existing?"1":"0");
                Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
                Assert.DoesNotContain("synthetic test failure",await response.Content.ReadAsStringAsync());
                Assert.Equal(before,await SnapshotAsync());
            }
            finally { await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b06_failure;"); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCreates_SerializeRevisionAndAuthoritativeCapOrName(bool atCap)
    {
        var (client, auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            if(atCap) await SeedPlansAsync(auth.userId,49);
            var responses=await Task.WhenAll(CreateAsync(client,Easy("Café")),CreateAsync(client,Easy(atCap?"Last slot":"cafe")));
            try
            {
                Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);
                await ErrorAsync(Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict),HttpStatusCode.Conflict,"meal_plan_revision_conflict");
                var list=await client.GetFromJsonAsync<NutritionPlanListDto>(Path);
                Assert.Equal(1,list!.Revision); Assert.Equal(atCap?50:1,list.Plans.Count);
                Assert.Equal(list.Settings.CurrentNutritionPlanId,list.Plans[0].Id);
                var before=await SnapshotAsync();
                var name=atCap?"Overflow":list.Plans[0].Name.ToUpperInvariant();
                using var fresh=await CreateAsync(client,Easy(name),"1");
                await ErrorAsync(fresh,HttpStatusCode.Conflict,atCap?"plan_limit_reached":"plan_name_conflict");
                Assert.Equal(before,await SnapshotAsync());
            }
            finally { foreach(var response in responses) response.Dispose(); }
        }
    }

    [Fact]
    public async Task ExistingPlansWithoutCurrent_NewPlanBecomesCurrent()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            await SeedPlansAsync(auth.userId,1);
            using var response=await CreateAsync(client,Easy("New Current")); response.EnsureSuccessStatusCode();
            var receipt=await response.Content.ReadFromJsonAsync<NutritionPlanMutationDto>();
            Assert.True(receipt!.PlannerInvalidated); Assert.Equal(receipt.Plan!.Id,receipt.Settings.CurrentNutritionPlanId);
            Assert.Equal(2,(await client.GetFromJsonAsync<NutritionPlanListDto>(Path))!.Plans.Count);
        }
    }

    [Fact]
    public async Task UnicodeNameBound_UsesNormalizedScalarsNotUtf16Length()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var name=string.Concat(Enumerable.Repeat("😀",80));
            using var valid=await CreateAsync(client,Easy(name,8)); valid.EnsureSuccessStatusCode();
            Assert.Equal(name,(await valid.Content.ReadFromJsonAsync<NutritionPlanMutationDto>())!.Plan!.Name);
            var before=await SnapshotAsync();
            using var invalid=await CreateAsync(client,Easy(name+"😀"),"1");
            await ErrorAsync(invalid,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("GET","/api/nutrition-plans?userId=1")]
    [InlineData("GET","/api/nutrition-plans/1?ownerId=1")]
    [InlineData("POST","/api/nutrition-plans?limit=20")]
    [InlineData("GET","/api/nutrition-plans/0")]
    [InlineData("GET","/api/nutrition-plans/-1")]
    public async Task ExtraQueriesOrInvalidIds_AreRejectedWithoutWrites(string method,string path)
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var before=await SnapshotAsync(); using var request=new HttpRequestMessage(new HttpMethod(method),path);
            if(method=="POST") { request.Content=JsonContent.Create(Easy()); request.Headers.Add("X-Meal-Plan-Revision","0"); }
            using var response=await client.SendAsync(request);
            await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("GET","/api/nutrition-plans")]
    [InlineData("GET","/api/nutrition-plans/1")]
    [InlineData("POST","/api/nutrition-plans")]
    public async Task AnonymousRequests_AreUnauthorizedAndNoStore(string method,string path)
    {
        var before=await SnapshotAsync(); using var request=new HttpRequestMessage(new HttpMethod(method),path);
        if(method=="POST") { request.Content=JsonContent.Create(Easy()); request.Headers.Add("X-Meal-Plan-Revision","0"); }
        using var response=await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(before,await SnapshotAsync());
    }

    [Fact]
    public async Task MissingCsrf_CannotCreatePlan()
    {
        using var client=Factory.CreateClient(new() {BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var signup=new HttpRequestMessage(HttpMethod.Post,"/api/auth/sign-up")
        { Content=JsonContent.Create(new SignUpRequest("B06 CSRF","b06-csrf@appetee.test","Password123!")) };
        signup.Headers.Add("Origin","https://localhost:4200"); signup.Headers.Add("X-CSRF-TOKEN",token.GetProperty("requestToken").GetString());
        using var signedIn=await client.SendAsync(signup); signedIn.EnsureSuccessStatusCode();
        var before=await SnapshotAsync(); using var request=new HttpRequestMessage(HttpMethod.Post,Path){Content=JsonContent.Create(Easy())};
        request.Headers.Add("Origin","https://localhost:4200"); request.Headers.Add("X-Meal-Plan-Revision","0");
        using var response=await client.SendAsync(request); await ErrorAsync(response,HttpStatusCode.BadRequest,"csrf_invalid");
        Assert.True(response.Headers.CacheControl!.NoStore); Assert.Equal(before,await SnapshotAsync());
    }

    [Fact]
    public async Task GetBodyAndUnsupportedPostMedia_AreRejectedAndPreviewRouteStillWorks()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var before=await SnapshotAsync();
            using var get=new HttpRequestMessage(HttpMethod.Get,Path){Content=JsonContent.Create(new { })};
            using var response=await client.SendAsync(get); await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed");
            using var post=new HttpRequestMessage(HttpMethod.Post,Path){Content=new StringContent("name=Plan",Encoding.UTF8,"application/x-www-form-urlencoded")};
            post.Headers.Add("X-Meal-Plan-Revision","0"); using var unsupported=await client.SendAsync(post);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType,unsupported.StatusCode);
            using var preview=await client.PostAsJsonAsync(Path+"/calculation-preview",
                new NutritionCalculationRequest(new("male",DateTime.UtcNow.Year-28,180m,80m),"maintain","active"));
            preview.EnsureSuccessStatusCode(); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task OpenApi_DeclaresStrictCreateAndSeparateSummaryDetailContracts()
    {
        using var json=JsonDocument.Parse(await Client.GetStringAsync("/swagger/v1/swagger.json"));
        var schemas=json.RootElement.GetProperty("components").GetProperty("schemas");
        var request=schemas.GetProperty("CreateNutritionPlanRequest");
        Assert.False(request.GetProperty("additionalProperties").GetBoolean()); Assert.Equal(3,request.GetProperty("required").GetArrayLength());
        Assert.Equal(6,request.GetProperty("properties").EnumerateObject().Count());
        Assert.Equal(2,request.GetProperty("oneOf").GetArrayLength());
        Assert.Equal(2,request.GetProperty("properties").GetProperty("mode").GetProperty("enum").GetArrayLength());
        Assert.Equal(11,schemas.GetProperty("NutritionPlanSummaryDto").GetProperty("properties").EnumerateObject().Count());
        Assert.Equal(15,schemas.GetProperty("NutritionPlanDto").GetProperty("properties").EnumerateObject().Count());
        foreach(var name in new[]{"NutritionPlanListDto","NutritionPlanDetailDto","NutritionPlanMutationDto"})
            Assert.Equal("string",schemas.GetProperty(name).GetProperty("properties").GetProperty("revision").GetProperty("type").GetString());
        var operations=json.RootElement.GetProperty("paths").GetProperty(Path);
        var parameters=operations.GetProperty("post").GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Contains(parameters,p=>p.GetProperty("name").GetString()=="X-Meal-Plan-Revision" && p.GetProperty("required").GetBoolean());
        Assert.Contains(parameters,p=>p.GetProperty("name").GetString()=="X-CSRF-TOKEN" && p.GetProperty("required").GetBoolean());
        Assert.DoesNotContain(parameters,p=>p.GetProperty("name").GetString() is "userId" or "ownerId");
    }

    [Fact]
    public async Task Explain_OwnerReadsAndDynamicChecksUseExistingIndexes()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            await SeedPlansAsync(auth.userId,50);
            await Factory.Database.ExecuteAsync("""
                INSERT INTO meal_plan_user_settings(user_id,current_nutrition_plan_id,revision,created_at,updated_at)
                SELECT @Id,MIN(id),0,'2026-10-01','2026-10-01' FROM nutrition_plans WHERE user_id=@Id;
                """,new {Id=auth.userId});
            // A one-owner 50-row table legitimately favors a tiny table scan. Use a
            // selective multi-owner fixture before assessing the production owner path.
            for(var i=0;i<10;i++)
            {
                var (other,otherAuth)=await CreateAuthenticatedClientAsync(); using(other)
                    await SeedPlansAsync(otherAuth.userId,50);
            }
            await Factory.Database.ExecuteAsync("ANALYZE TABLE nutrition_plans,meal_plan_user_settings;");
            foreach(var sql in new[]{NutritionPlanSql.List,NutritionPlanSql.Detail,NutritionPlanSql.Current,NutritionPlanSql.Count,NutritionPlanSql.NameExists})
            {
                var explain=await Factory.Database.QueryAsync<ExplainRow>("EXPLAIN "+sql,new {CurrentUserId=auth.userId,PlanId=1,Name="Seed 1"});
                Assert.NotEmpty(explain);
                Assert.Contains(explain,r=>r.Key is not null);
                Assert.DoesNotContain(explain,r=>r.Type=="ALL" && r.Table is "p" or "nutrition_plans");
            }
        }
    }

    private sealed record ExplainRow { public string? Table {get;init;} public string? Type {get;init;} public string? Key {get;init;} }

    [Fact]
    public async Task NonCurrentIntermediate_WithDifferentProfile_DoesNotInvalidateCurrentPlanner()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            using var first=await CreateAsync(client,Easy()); first.EnsureSuccessStatusCode();
            var current=(await first.Content.ReadFromJsonAsync<NutritionPlanMutationDto>())!.Settings;
            using var next=await CreateAsync(client,Intermediate(),"1"); next.EnsureSuccessStatusCode();
            var receipt=await next.Content.ReadFromJsonAsync<NutritionPlanMutationDto>();
            Assert.NotNull(receipt!.Profile); Assert.False(receipt.PlannerInvalidated); Assert.Equal(current,receipt.Settings);
            Assert.Equal(2,receipt.Revision);
        }
    }

    [Fact]
    public async Task YearRollover_ReadPreservesStoredAgeTargetsWarningsAndProfileComparison()
    {
        var clock=new AdjustableClock(new DateTimeOffset(2029,12,31,23,59,59,TimeSpan.Zero));
        using var customized=Factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>
        { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); }));
        using var client=AppeteeWebApplicationFactory.CreateCsrfClient(customized);
        using var signup=await client.PostAsJsonAsync("/api/auth/sign-up",new SignUpRequest("B06 clock","b06-clock@appetee.test","Password123!"));
        signup.EnsureSuccessStatusCode();
        var body=new {name="Stored provenance",mode="intermediate",defaultMealsPerDay=3,
            calculation=new NutritionCalculationRequest(new("male",1994,180m,80m),"maintain","active"),
            reviewedPolicyVersion=NutritionPolicy.Version,reviewedCalculationYear=2029};
        using var created=await CreateAsync(client,body); created.EnsureSuccessStatusCode();
        var original=(await created.Content.ReadFromJsonAsync<NutritionPlanMutationDto>())!.Plan!;
        Assert.Equal(35,original.CalculationInputs!.AgeYears); Assert.DoesNotContain(NutritionPolicy.PopulationWarning,original.Warnings);
        clock.Now=clock.Now.AddSeconds(2);
        var before=await SnapshotAsync();
        var detail=await client.GetFromJsonAsync<NutritionPlanDetailDto>($"{Path}/{original.Id}");
        Assert.False(detail!.ProfileDiffers); Assert.Equal(original.CalculationInputs,detail.Plan.CalculationInputs);
        Assert.Equal(original.Targets,detail.Plan.Targets); Assert.Equal(original.Warnings,detail.Plan.Warnings);
        Assert.Equal(original.CalculatedAt,detail.Plan.CalculatedAt); Assert.Equal(before,await SnapshotAsync());
        using var stale=await CreateAsync(client,WithName("Stale"),"1");
        await ErrorAsync(stale,HttpStatusCode.Conflict,"calculation_preview_stale"); Assert.Equal(before,await SnapshotAsync());
        object WithName(string name)=>new { name,body.mode,body.defaultMealsPerDay,body.calculation,body.reviewedPolicyVersion,body.reviewedCalculationYear };
    }

    private sealed class AdjustableClock(DateTimeOffset now):TimeProvider
    { public DateTimeOffset Now {get;set;}=now; public override DateTimeOffset GetUtcNow()=>Now; }

    [Fact]
    public async Task YearChangesWhileWaitingForOwnerLock_RejectsReviewedPreviewAndRollsBack()
    {
        var clock=new AdjustableClock(new DateTimeOffset(2029,12,31,23,59,59,TimeSpan.Zero));
        var observed=new ObservedCalculator(clock);
        using var customized=Factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>
        {
            s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock);
            s.RemoveAll<INutritionCalculationService>(); s.AddSingleton<INutritionCalculationService>(observed);
        }));
        using var client=AppeteeWebApplicationFactory.CreateCsrfClient(customized);
        using var signup=await client.PostAsJsonAsync("/api/auth/sign-up",new SignUpRequest("B06 lock","b06-lock@appetee.test","Password123!"));
        signup.EnsureSuccessStatusCode();
        var owner=(await signup.Content.ReadFromJsonAsync<Appetee.Application.Models.Auth.AuthResult>())!.userId;
        var before=await SnapshotAsync();
        using var scope=customized.Services.CreateScope();
        var transactions=scope.ServiceProvider.GetRequiredService<MealPlanTransaction>();
        var locked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder=transactions.ExecuteAsync("test.b06.year-lock",owner,0,async context=>
        {
            locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return MealPlanMutation<string>.UnchangedResult("held");
        },CancellationToken.None);
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage>? pending=null;
        try
        {
            pending=CreateAsync(client,new {name="Rollover",mode="intermediate",defaultMealsPerDay=3,
                calculation=new NutritionCalculationRequest(new("male",2001,180m,80m),"maintain","active"),
                reviewedPolicyVersion=NutritionPolicy.Version,reviewedCalculationYear=2029});
            await observed.Calculated.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Now=clock.Now.AddSeconds(2); release.TrySetResult(); await holder;
            using var response=await pending;
            await ErrorAsync(response,HttpStatusCode.Conflict,"calculation_preview_stale");
            Assert.Equal(before,await SnapshotAsync());
        }
        finally
        {
            release.TrySetResult(); await holder;
            if(pending is not null) (await pending).Dispose();
        }
    }

    private sealed class ObservedCalculator(TimeProvider clock):INutritionCalculationService
    {
        public TaskCompletionSource Calculated {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public NutritionCalculationDto Calculate(NutritionCalculationRequest request,CancellationToken ct=default)
        {
            var result=new NutritionCalculationService(clock).Calculate(request,ct);
            Calculated.TrySetResult(); return result;
        }
    }

    [Fact]
    public async Task BoundaryLogging_ReportsActualStatusWithoutSubmittedNamesOrProfiles()
    {
        using var logs=new BoundaryLogProvider();
        using var customized=Factory.WithWebHostBuilder(b=>b.ConfigureLogging(l=>l.AddProvider(logs)));
        using var client=AppeteeWebApplicationFactory.CreateCsrfClient(customized);
        using var signup=await client.PostAsJsonAsync("/api/auth/sign-up",new SignUpRequest("B06 logs","b06-logs@appetee.test","Password123!"));
        signup.EnsureSuccessStatusCode();
        using var created=await CreateAsync(client,Intermediate("Private submitted name")); created.EnsureSuccessStatusCode();
        var before=await SnapshotAsync();
        using var request=new HttpRequestMessage(HttpMethod.Post,Path)
        {Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"name","Private submitted name"}})};
        request.Headers.Add("X-Meal-Plan-Revision","1");
        using var rejected=await client.SendAsync(request); Assert.Equal(HttpStatusCode.UnsupportedMediaType,rejected.StatusCode);
        // Consumes may reject 415 before action filters run; the common HTTP
        // pipeline owns that diagnostic. Exercise a rejection owned by this boundary.
        using var invalid=await CreateAsync(client,Easy("Private submitted name",9),"1");
        await ErrorAsync(invalid,HttpStatusCode.BadRequest,"validation_failed");
        Assert.Contains(logs.Messages,m=>m.Contains("status 201; outcome completed") && m.Contains("plan rows 1") && m.Contains("trace "));
        Assert.Contains(logs.Messages,m=>m.Contains("status 400; outcome validation_failed") && m.Contains("plan rows 0"));
        Assert.DoesNotContain(logs.Messages,m=>m.Contains("Private submitted name") || m.Contains("b06-logs@appetee.test")
            || m.Contains("weightKg") || m.Contains("heightCm") || m.Contains("birthYear"));
        Assert.Equal(before,await SnapshotAsync());
    }

    private sealed class BoundaryLogProvider:ILoggerProvider
    {
        public ConcurrentQueue<string> Messages {get;}=new();
        public ILogger CreateLogger(string categoryName)=>new BoundaryLogger(Messages,
            categoryName=="Appetee.Api.Helpers.NutritionPlanBoundaryAttribute");
        public void Dispose() { }
        private sealed class BoundaryLogger(ConcurrentQueue<string> messages,bool enabled):ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
            public bool IsEnabled(LogLevel level)=>enabled;
            public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
            { if(enabled) messages.Enqueue(formatter(state,exception)); }
        }
    }

    private Task SeedPlansAsync(int owner,int count)=>Factory.Database.ExecuteAsync(
        "INSERT INTO nutrition_plans(user_id,name,mode,default_meals_per_day,created_at,updated_at) VALUES "+
        string.Join(",",Enumerable.Range(1,count).Select(i=>$"(@Owner,'Seed {i}','Easy',3,'2026-10-01','2026-10-01')"))+";",new{Owner=owner});

    private Task<string?> ProfileSnapshotAsync(int id)=>Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_OBJECT('sex',calculation_sex,'year',birth_year,'height',height_cm,'weight',weight_kg,'updated',updated_at) AS CHAR)
        FROM users WHERE id=@Id;
        """,new {Id=id});
    private Task<string?> SnapshotAsync()=>Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_OBJECT(
            'users',(SELECT JSON_ARRAYAGG(JSON_OBJECT('id',id,'sex',calculation_sex,'year',birth_year,'height',height_cm,'weight',weight_kg,'updated',updated_at)) FROM users),
            'settings',(SELECT JSON_ARRAYAGG(JSON_OBJECT('id',user_id,'current',current_nutrition_plan_id,'revision',revision,'created',created_at,'updated',updated_at)) FROM meal_plan_user_settings),
            'plans',(SELECT JSON_ARRAYAGG(JSON_OBJECT('id',id,'owner',user_id,'name',name,'mode',mode,'count',default_meals_per_day,
                'inputs',JSON_ARRAY(goal,activity_level,calculation_sex,birth_year,age_years,height_cm,weight_kg,calculation_policy_version,rmr_kcal,maintenance_kcal),
                'targets',JSON_ARRAY(target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g),'calculated',calculated_at,'created',created_at,'updated',updated_at)) FROM nutrition_plans),
            'days',(SELECT COUNT(*) FROM meal_plan_days),'meals',(SELECT COUNT(*) FROM meal_plan_day_meals),'items',(SELECT COUNT(*) FROM meal_plan_meal_items)) AS CHAR);
        """);
    private static async Task<JsonElement> ErrorAsync(HttpResponseMessage response,HttpStatusCode status,string code)
    {
        var text=await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode==status,$"Expected {status}, got {response.StatusCode}: {text}");
        using var json=JsonDocument.Parse(text); Assert.Equal(code,json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId",out _)); return json.RootElement.Clone();
    }
}
