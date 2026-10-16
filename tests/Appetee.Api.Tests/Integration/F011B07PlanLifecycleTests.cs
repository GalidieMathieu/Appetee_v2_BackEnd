using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.Services.MealPlans;
using Appetee.Infrastructure.MealPlans;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B07PlanLifecycleTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Plans = "/api/nutrition-plans";
    private const string Current = "/api/meal-plan/current-nutrition-plan";
    private static object Easy(string name="Easy", int count=3) => new {name,mode="easy",defaultMealsPerDay=count};
    private static object Metadata(string name, int count=3) => new {name,defaultMealsPerDay=count};
    private static object Calculation(string name="Athlete", int count=3, decimal weight=80m) => new
    {
        name,defaultMealsPerDay=count,
        calculation=new NutritionCalculationRequest(new("male",DateTime.UtcNow.Year-28,180m,weight),"maintain","active"),
        reviewedPolicyVersion=NutritionPolicy.Version,reviewedCalculationYear=DateTime.UtcNow.Year,
    };
    private static object Intermediate(string name="Athlete")
    { var node=JsonSerializer.SerializeToNode(Calculation(name))!.AsObject(); node["mode"]="intermediate"; return node; }
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method,string path,object? body=null,string? revision="0")
    {
        using var request=new HttpRequestMessage(new HttpMethod(method),path);
        if(body is not null) request.Content=JsonContent.Create(body);
        if(revision is not null) request.Headers.TryAddWithoutValidation("X-Meal-Plan-Revision",revision);
        return await client.SendAsync(request);
    }
    private static async Task<NutritionPlanMutationDto> ReceiptAsync(HttpResponseMessage response,HttpStatusCode status=HttpStatusCode.OK)
    {
        var text=await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode==status,$"Expected {status}, got {response.StatusCode}: {text}");
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var json=JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.String,json.RootElement.GetProperty("revision").ValueKind);
        Assert.Equal(6,json.RootElement.EnumerateObject().Count());
        return JsonSerializer.Deserialize<NutritionPlanMutationDto>(text,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private static async Task<NutritionPlanMutationDto> CreateAsync(HttpClient client,object body,string revision="0")
    { using var response=await SendAsync(client,"POST",Plans,body,revision); return await ReceiptAsync(response,HttpStatusCode.Created); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataUpdate_PreservesAllCalculationValuesProfileAndSavedFood(bool active)
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var source=await CreateAsync(client,Intermediate());
            if(!active) { using var clear=await SendAsync(client,"POST",$"{Plans}/{source.Plan!.Id}/duplicate",revision:"1");
                source=await ReceiptAsync(clear,HttpStatusCode.Created); }
            await SeedGraphAsync(auth.userId);
            var id=source.Plan!.Id; var calculation=await CalculationSnapshotAsync(id); var graph=await GraphAsync(); var profile=await ProfileAsync(auth.userId);
            using var response=await SendAsync(client,"PUT",$"{Plans}/{id}",Metadata("Renamed",4),source.Revision.ToString());
            var receipt=await ReceiptAsync(response);
            Assert.Equal(source.Revision+1,receipt.Revision); Assert.Equal("Renamed",receipt.Plan!.Name); Assert.Equal(4,receipt.Plan.DefaultMealsPerDay);
            Assert.Equal(active,receipt.PlannerInvalidated); Assert.Null(receipt.Profile);
            Assert.Equal(calculation,await CalculationSnapshotAsync(id)); Assert.Equal(graph,await GraphAsync()); Assert.Equal(profile,await ProfileAsync(auth.userId));
            Assert.Equal(source.Plan.CreatedAt,receipt.Plan.CreatedAt);
        }
    }

    [Fact]
    public async Task SameMetadataAndSameCurrent_AreExactNoOpsButStillRequireCurrentRevision()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Intermediate()); var id=first.Plan!.Id; var before=await SnapshotAsync();
            using var update=await SendAsync(client,"PUT",$"{Plans}/{id}",Metadata(first.Plan.Name),"1");
            var same=await ReceiptAsync(update); Assert.Equal(1,same.Revision); Assert.False(same.PlannerInvalidated); Assert.Null(same.Profile);
            using var select=await SendAsync(client,"PUT",Current,new {nutritionPlanId=id},"1");
            var selected=await ReceiptAsync(select); Assert.Equal(1,selected.Revision); Assert.False(selected.PlannerInvalidated);
            Assert.Equal(before,await SnapshotAsync());
            using var stale=await SendAsync(client,"PUT",Current,new {nutritionPlanId=id},"0");
            await ErrorAsync(stale,HttpStatusCode.Conflict,"meal_plan_revision_conflict"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task MetadataNoOp_OnLegacyPlanDoesNotLeaveProvisionalSettings()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            await SeedPlansAsync(auth.userId,1);
            var id=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM nutrition_plans WHERE user_id=@Id;",new{Id=auth.userId});
            var before=await SnapshotAsync(); using var update=await SendAsync(client,"PUT",$"{Plans}/{id}",Metadata("Seed 1"));
            var receipt=await ReceiptAsync(update); Assert.Equal(0,receipt.Revision); Assert.Null(receipt.Settings.CurrentPlan);
            Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task CurrentNameOnlyEdit_DoesNotInvalidatePlanner()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy());
            using var update=await SendAsync(client,"PUT",$"{Plans}/{first.Plan!.Id}",Metadata("Name only"),"1");
            var receipt=await ReceiptAsync(update); Assert.False(receipt.PlannerInvalidated); Assert.Equal("Name only",receipt.Settings.CurrentPlan!.Name);
            Assert.Equal(2,receipt.Revision);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitRecalculation_UpdatesProfileAndPlanAtomically_OnlyActiveTargetsInvalidate(bool active)
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Intermediate());
            if(!active) { using var copy=await SendAsync(client,"POST",$"{Plans}/{first.Plan!.Id}/duplicate",revision:"1"); first=await ReceiptAsync(copy,HttpStatusCode.Created); }
            await SeedGraphAsync(auth.userId); var graph=await GraphAsync(); var before=await CalculationSnapshotAsync(first.Plan!.Id);
            using var update=await SendAsync(client,"PUT",$"{Plans}/{first.Plan.Id}",Calculation(first.Plan.Name,weight:85m),first.Revision.ToString());
            var receipt=await ReceiptAsync(update);
            Assert.Equal(first.Revision+1,receipt.Revision); Assert.Equal(85m,receipt.Plan!.CalculationInputs!.WeightKg); Assert.Equal(85m,receipt.Profile!.WeightKg);
            Assert.Equal(active,receipt.PlannerInvalidated); Assert.NotEqual(before,await CalculationSnapshotAsync(first.Plan.Id));
            Assert.Equal(receipt.Plan.CalculatedAt,receipt.Plan.UpdatedAt); Assert.Equal(first.Plan.CreatedAt,receipt.Plan.CreatedAt);
            Assert.Equal(graph,await GraphAsync());
            using var repeat=await SendAsync(client,"PUT",$"{Plans}/{first.Plan.Id}",Calculation(first.Plan.Name,weight:85m),receipt.Revision.ToString());
            var recalculated=await ReceiptAsync(repeat); Assert.Equal(receipt.Revision+1,recalculated.Revision);
            Assert.False(recalculated.PlannerInvalidated); Assert.Null(recalculated.Profile);
        }
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("revision")]
    public async Task RecalculationInjectedFailure_RollsBackProfilePlanSettingsAndGraph(string stage)
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Intermediate()); await SeedGraphAsync(auth.userId); var before=await SnapshotAsync();
            var sql=stage=="plan"
                ? "CREATE TRIGGER b07_fail AFTER UPDATE ON nutrition_plans FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic failure';"
                : "CREATE TRIGGER b07_fail BEFORE UPDATE ON meal_plan_user_settings FOR EACH ROW BEGIN IF NEW.revision>OLD.revision THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic failure'; END IF; END;";
            await Factory.Database.ExecuteAsync(sql);
            try
            {
                using var update=await SendAsync(client,"PUT",$"{Plans}/{first.Plan!.Id}",Calculation(weight:85m),"1");
                Assert.Equal(HttpStatusCode.InternalServerError,update.StatusCode); Assert.DoesNotContain("synthetic failure",await update.Content.ReadAsStringAsync());
                Assert.Equal(before,await SnapshotAsync());
            }
            finally { await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b07_fail;"); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Duplicate_PreservesProvenanceAndProfile_RemainsInactiveEvenWhenCurrentIsNull(bool noCurrent)
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var source=await CreateAsync(client,Intermediate());
            await Factory.Database.ExecuteAsync("UPDATE nutrition_plans SET created_at='2026-01-01',updated_at='2026-01-02',calculated_at='2026-01-01',calculation_policy_version='previous-policy' WHERE id=@Id;",new {Id=source.Plan!.Id});
            if(noCurrent) await Factory.Database.ExecuteAsync("UPDATE meal_plan_user_settings SET current_nutrition_plan_id=NULL WHERE user_id=@Id;",new{Id=auth.userId});
            var calculation=await CalculationSnapshotAsync(source.Plan.Id); var profile=await ProfileAsync(auth.userId);
            using var response=await SendAsync(client,"POST",$"{Plans}/{source.Plan.Id}/duplicate",revision:"1");
            var receipt=await ReceiptAsync(response,HttpStatusCode.Created);
            Assert.Equal(2,receipt.Revision); Assert.NotEqual(source.Plan.Id,receipt.Plan!.Id); Assert.Equal("Athlete - Copy",receipt.Plan.Name);
            Assert.Equal($"{Plans}/{receipt.Plan.Id}",response.Headers.Location!.OriginalString);
            Assert.Equal(calculation,await CalculationSnapshotAsync(receipt.Plan.Id)); Assert.Equal(profile,await ProfileAsync(auth.userId));
            Assert.False(receipt.PlannerInvalidated); Assert.Null(receipt.Profile);
            Assert.Equal(noCurrent?(int?)null:source.Plan.Id,receipt.Settings.CurrentNutritionPlanId);
            Assert.Equal(receipt.Plan.CreatedAt,receipt.Plan.UpdatedAt); Assert.True(receipt.Plan.CreatedAt>new DateTime(2026,1,2));
        }
    }

    [Fact]
    public async Task DuplicateNaming_RespectsUnicodeScalarBoundAndDatabaseCollation()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var longName=string.Concat(Enumerable.Repeat("😀",80));
            var source=await CreateAsync(client,Easy(longName));
            var occupied=string.Concat(Enumerable.Repeat("😀",73))+" - copy";
            await CreateAsync(client,Easy(occupied),"1");
            using var response=await SendAsync(client,"POST",$"{Plans}/{source.Plan!.Id}/duplicate",revision:"2");
            var receipt=await ReceiptAsync(response,HttpStatusCode.Created);
            Assert.Equal(string.Concat(Enumerable.Repeat("😀",71))+" - Copy 2",receipt.Plan!.Name);
            Assert.Equal(80,receipt.Plan.Name.EnumerateRunes().Count()); Assert.Equal(source.Plan.Id,receipt.Settings.CurrentNutritionPlanId);
            Assert.Null(receipt.Plan.CalculationInputs); Assert.Null(receipt.Plan.Targets);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDuplicate_SerializesRevision_AndFreshRetryUsesNextSuffixOrCap(bool atCap)
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy("Café")); var path=$"{Plans}/{first.Plan!.Id}/duplicate";
            if(atCap) await SeedPlansAsync(auth.userId,48);
            var responses=await Task.WhenAll(SendAsync(client,"POST",path,revision:"1"),SendAsync(client,"POST",path,revision:"1"));
            try
            {
                await ReceiptAsync(Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created),HttpStatusCode.Created);
                await ErrorAsync(Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict),HttpStatusCode.Conflict,"meal_plan_revision_conflict");
                using var fresh=await SendAsync(client,"POST",path,revision:"2");
                if(atCap) await ErrorAsync(fresh,HttpStatusCode.Conflict,"plan_limit_reached");
                else Assert.Equal("Café - Copy 2",(await ReceiptAsync(fresh,HttpStatusCode.Created)).Plan!.Name);
                Assert.Equal(atCap?50:3,(await client.GetFromJsonAsync<NutritionPlanListDto>(Plans))!.Plans.Count);
            }
            finally { foreach(var response in responses) response.Dispose(); }
        }
    }

    [Fact]
    public async Task DuplicateAtCap_Is409AndLeavesAllStateUnchanged()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy()); await SeedPlansAsync(auth.userId,49);
            var before=await SnapshotAsync(); using var response=await SendAsync(client,"POST",$"{Plans}/{first.Plan!.Id}/duplicate",revision:"1");
            await ErrorAsync(response,HttpStatusCode.Conflict,"plan_limit_reached"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_ReturnsReceipt_ClearsOnlyCurrentPointer_AndPreservesAllFood(bool active)
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Intermediate()); var second=await CreateAsync(client,Easy("Second"),"1");
            await SeedGraphAsync(auth.userId); var graph=await GraphAsync(); var profile=await ProfileAsync(auth.userId);
            var id=active?first.Plan!.Id:second.Plan!.Id;
            using var response=await SendAsync(client,"DELETE",$"{Plans}/{id}",revision:"2"); var receipt=await ReceiptAsync(response);
            Assert.Equal(3,receipt.Revision); Assert.Null(receipt.Plan); Assert.Equal(id,receipt.DeletedPlanId); Assert.Null(receipt.Profile);
            Assert.Equal(active,receipt.PlannerInvalidated); Assert.Equal(active?(int?)null:first.Plan!.Id,receipt.Settings.CurrentNutritionPlanId);
            Assert.Equal(graph,await GraphAsync()); Assert.Equal(profile,await ProfileAsync(auth.userId));
            Assert.Single((await client.GetFromJsonAsync<NutritionPlanListDto>(Plans))!.Plans);
            var before=await SnapshotAsync(); using var repeated=await SendAsync(client,"DELETE",$"{Plans}/{id}",revision:"2");
            await ErrorAsync(repeated,HttpStatusCode.NotFound,"resource_not_found"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task CurrentDeletionFailure_RestoresPointerPlanAndFood()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy()); await SeedGraphAsync(auth.userId); var before=await SnapshotAsync();
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b07_fail AFTER DELETE ON nutrition_plans FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic failure';");
            try
            {
                using var response=await SendAsync(client,"DELETE",$"{Plans}/{first.Plan!.Id}",revision:"1"); Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
                Assert.Equal(before,await SnapshotAsync());
            }
            finally { await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b07_fail;"); }
        }
    }

    [Fact]
    public async Task SetCurrent_UsesStoredTargetsDespiteProfileMismatchAndDoesNotRewriteAnyPlanOrFood()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var easy=await CreateAsync(client,Easy()); var athlete=await CreateAsync(client,Intermediate(),"1");
            await Factory.Database.ExecuteAsync("UPDATE users SET weight_kg=85 WHERE id=@Id;",new{Id=auth.userId});
            await SeedGraphAsync(auth.userId); var calc=await CalculationSnapshotAsync(athlete.Plan!.Id); var graph=await GraphAsync(); var profile=await ProfileAsync(auth.userId);
            using var response=await SendAsync(client,"PUT",Current,new{nutritionPlanId=athlete.Plan.Id},"2"); var receipt=await ReceiptAsync(response);
            Assert.Equal(3,receipt.Revision); Assert.True(receipt.PlannerInvalidated); Assert.Null(receipt.Profile);
            Assert.Equal(athlete.Plan.Targets,receipt.Settings.CurrentPlan!.Targets); Assert.Equal(athlete.Plan.UpdatedAt,receipt.Plan!.UpdatedAt);
            Assert.Equal(calc,await CalculationSnapshotAsync(athlete.Plan.Id)); Assert.Equal(graph,await GraphAsync()); Assert.Equal(profile,await ProfileAsync(auth.userId));
            Assert.True((await client.GetFromJsonAsync<NutritionPlanDetailDto>($"{Plans}/{athlete.Plan.Id}"))!.ProfileDiffers);
            using var back=await SendAsync(client,"PUT",Current,new{nutritionPlanId=easy.Plan!.Id},"3");
            Assert.Null((await ReceiptAsync(back)).Settings.CurrentPlan!.Targets);
        }
    }

    [Theory]
    [InlineData("update")]
    [InlineData("duplicate")]
    [InlineData("delete")]
    [InlineData("current")]
    public async Task MissingAndForeignTargets_AreUniform404EvenWithStaleRevision(string operation)
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); var (other,_)=await CreateAuthenticatedClientAsync(); using(client) using(other)
        {
            var foreign=await CreateAsync(other,Easy()); var before=await SnapshotAsync();
            foreach(var id in new[]{foreign.Plan!.Id,int.MaxValue})
            {
                using var response=await OperationAsync(client,operation,id,"99");
                await ErrorAsync(response,HttpStatusCode.NotFound,"resource_not_found"); Assert.Equal(before,await SnapshotAsync());
            }
            var owner=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT user_id FROM nutrition_plans WHERE id=@Id;",new{Id=foreign.Plan.Id});
            // Direct bypass of service ownership is still rejected by the composite FK.
            var own=await CreateAsync(client,Easy()); var after=await SnapshotAsync();
            var ownOwner=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT user_id FROM nutrition_plans WHERE id=@Id;",new{Id=own.Plan!.Id});
            var error=await Assert.ThrowsAsync<MySqlException>(()=>Factory.Database.ExecuteAsync(
                "UPDATE meal_plan_user_settings SET current_nutrition_plan_id=@Plan WHERE user_id=@Owner;",new {Plan=foreign.Plan.Id,Owner=ownOwner}));
            Assert.Equal(1452,error.Number); Assert.NotEqual(owner,ownOwner); Assert.Equal(after,await SnapshotAsync());
        }
    }

    private static Task<HttpResponseMessage> OperationAsync(HttpClient client,string operation,int id,string? revision="0")=>operation switch
    {
        "update"=>SendAsync(client,"PUT",$"{Plans}/{id}",Metadata("Edited"),revision),
        "duplicate"=>SendAsync(client,"POST",$"{Plans}/{id}/duplicate",revision:revision),
        "delete"=>SendAsync(client,"DELETE",$"{Plans}/{id}",revision:revision),
        _=>SendAsync(client,"PUT",Current,new {nutritionPlanId=id},revision),
    };

    [Theory]
    [InlineData("update")]
    [InlineData("duplicate")]
    [InlineData("delete")]
    [InlineData("current")]
    public async Task StaleTabCannotMutateEditedPlan(string operation)
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy());
            using var updated=await SendAsync(client,"PUT",$"{Plans}/{first.Plan!.Id}",Metadata("New name"),"1"); await ReceiptAsync(updated);
            var before=await SnapshotAsync(); using var stale=await OperationAsync(client,operation,first.Plan.Id,"1");
            await ErrorAsync(stale,HttpStatusCode.Conflict,"meal_plan_revision_conflict"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("name","null")]
    [InlineData("name","REMOVE")]
    [InlineData("name","\"bad\\nname\"")]
    [InlineData("defaultMealsPerDay","REMOVE")]
    [InlineData("defaultMealsPerDay","null")]
    [InlineData("defaultMealsPerDay","\"3\"")]
    [InlineData("defaultMealsPerDay","3.5")]
    [InlineData("defaultMealsPerDay","9")]
    [InlineData("mode","\"easy\"")]
    [InlineData("userId","1")]
    [InlineData("ownerId","1")]
    [InlineData("targets","{}")]
    [InlineData("revision","\"1\"")]
    [InlineData("calculatedAt","\"2026-01-01\"")]
    [InlineData("calculation","null")]
    [InlineData("reviewedPolicyVersion","null")]
    [InlineData("reviewedCalculationYear","null")]
    public async Task StrictUpdate_RejectsMalformedOverpostedOrPartialCalculationWithoutWrites(string field,string value)
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Intermediate()); var body=JsonSerializer.SerializeToNode(Metadata("Edited"))!.AsObject();
            if(value=="REMOVE") body.Remove(field); else body[field]=JsonNode.Parse(value);
            var before=await SnapshotAsync(); using var response=await SendAsync(client,"PUT",$"{Plans}/{first.Plan!.Id}",body,"1");
            await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("nutritionPlanId","null")]
    [InlineData("nutritionPlanId","REMOVE")]
    [InlineData("nutritionPlanId","0")]
    [InlineData("nutritionPlanId","-1")]
    [InlineData("nutritionPlanId","\"1\"")]
    [InlineData("nutritionPlanId","1.5")]
    [InlineData("nutritionPlanId","2147483648")]
    [InlineData("ownerId","1")]
    [InlineData("userId","1")]
    [InlineData("mode","\"easy\"")]
    public async Task StrictCurrent_RejectsInvalidOrOverpostedFieldsWithoutWrites(string field,string value)
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy()); var body=JsonSerializer.SerializeToNode(new{nutritionPlanId=first.Plan!.Id})!.AsObject();
            if(value=="REMOVE") body.Remove(field); else body[field]=JsonNode.Parse(value);
            var before=await SnapshotAsync(); using var response=await SendAsync(client,"PUT",Current,body,"1");
            await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task EasyRejectsCompleteRecalculation_AndIntermediateRejectsStaleReviewAndInvalidCalculation()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var easy=await CreateAsync(client,Easy()); var before=await SnapshotAsync();
            using var invalid=await SendAsync(client,"PUT",$"{Plans}/{easy.Plan!.Id}",Calculation(),"1");
            await ErrorAsync(invalid,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
            var athlete=await CreateAsync(client,Intermediate(),"1"); before=await SnapshotAsync();
            foreach(var field in new[]{"reviewedPolicyVersion","reviewedCalculationYear","calculation"})
            {
                var body=JsonSerializer.SerializeToNode(Calculation(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
                if(field=="reviewedPolicyVersion") body[field]="old-policy";
                else if(field=="reviewedCalculationYear") body[field]=DateTime.UtcNow.Year-1;
                else body["calculation"]!["profile"]!["heightCm"]=119;
                using var response=await SendAsync(client,"PUT",$"{Plans}/{athlete.Plan!.Id}",body,"2");
                await ErrorAsync(response,field=="calculation"?HttpStatusCode.BadRequest:HttpStatusCode.Conflict,
                    field=="calculation"?"validation_failed":"calculation_preview_stale"); Assert.Equal(before,await SnapshotAsync());
            }
        }
    }

    [Fact]
    public async Task NameConflict_IsCaseAccentInsensitive_ButCaseOnlyRenameOfSelfIsValid()
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy("Café")); var second=await CreateAsync(client,Easy("Other"),"1"); var before=await SnapshotAsync();
            using var conflict=await SendAsync(client,"PUT",$"{Plans}/{second.Plan!.Id}",Metadata("CAFE"),"2");
            await ErrorAsync(conflict,HttpStatusCode.Conflict,"plan_name_conflict"); Assert.Equal(before,await SnapshotAsync());
            using var rename=await SendAsync(client,"PUT",$"{Plans}/{first.Plan!.Id}",Metadata("CAFÉ"),"2");
            var receipt=await ReceiptAsync(rename); Assert.Equal("CAFÉ",receipt.Plan!.Name); Assert.Equal(3,receipt.Revision); Assert.False(receipt.PlannerInvalidated);
        }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("delete")]
    public async Task NoBodyActions_RejectEvenEmptyJsonBodies(string operation)
    {
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy()); var before=await SnapshotAsync();
            using var response=await SendAsync(client,operation=="duplicate"?"POST":"DELETE",
                $"{Plans}/{first.Plan!.Id}"+(operation=="duplicate"?"/duplicate":""),new{},"1");
            await ErrorAsync(response,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("update")]
    [InlineData("duplicate")]
    [InlineData("delete")]
    [InlineData("current")]
    public async Task SecurityAndBoundaries_RequireAuthRevisionAndRejectQueries(string operation)
    {
        var before=await SnapshotAsync(); using var anonymous=await OperationAsync(Client,operation,1);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode); Assert.True(anonymous.Headers.CacheControl!.NoStore); Assert.Equal(before,await SnapshotAsync());
        var (client,_)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy()); before=await SnapshotAsync();
            using var absent=await OperationAsync(client,operation,first.Plan!.Id,null); await ErrorAsync(absent,HttpStatusCode.BadRequest,"revision_required");
            using var malformed=await OperationAsync(client,operation,first.Plan.Id,"1.0"); await ErrorAsync(malformed,HttpStatusCode.BadRequest,"revision_invalid");
            var path=operation=="current"?Current:$"{Plans}/{first.Plan.Id}"+(operation=="duplicate"?"/duplicate":"");
            using var query=await SendAsync(client,operation=="duplicate"?"POST":operation=="delete"?"DELETE":"PUT",path+"?ownerId=1",
                operation=="current"?new{nutritionPlanId=first.Plan.Id}:operation=="update"?Metadata("Edited"):null,"1");
            await ErrorAsync(query,HttpStatusCode.BadRequest,"validation_failed"); Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task OpenApi_DeclaresLifecycleContractsAndRequiredSecurityHeaders()
    {
        using var json=JsonDocument.Parse(await Client.GetStringAsync("/swagger/v1/swagger.json"));
        var schemas=json.RootElement.GetProperty("components").GetProperty("schemas");
        var update=schemas.GetProperty("UpdateNutritionPlanRequest"); Assert.False(update.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(5,update.GetProperty("properties").EnumerateObject().Count()); Assert.Equal(2,update.GetProperty("required").GetArrayLength()); Assert.Equal(2,update.GetProperty("oneOf").GetArrayLength());
        var current=schemas.GetProperty("SetCurrentNutritionPlanRequest"); Assert.False(current.GetProperty("additionalProperties").GetBoolean());
        Assert.Single(current.GetProperty("properties").EnumerateObject()); Assert.Equal(1,current.GetProperty("properties").GetProperty("nutritionPlanId").GetProperty("minimum").GetInt32());
        var paths=json.RootElement.GetProperty("paths");
        foreach(var (path,method) in new[]{(Plans+"/{planId}","put"),(Plans+"/{planId}","delete"),(Plans+"/{planId}/duplicate","post"),(Current,"put")})
        {
            var parameters=paths.GetProperty(path).GetProperty(method).GetProperty("parameters").EnumerateArray().ToArray();
            foreach(var name in new[]{"X-CSRF-TOKEN","Origin","X-Meal-Plan-Revision"})
                Assert.Contains(parameters,p=>p.GetProperty("name").GetString()==name && p.GetProperty("required").GetBoolean());
            Assert.DoesNotContain(parameters,p=>p.GetProperty("name").GetString() is "userId" or "ownerId");
        }
    }

    [Theory]
    [InlineData("update")]
    [InlineData("duplicate")]
    [InlineData("delete")]
    [InlineData("current")]
    public async Task MissingCsrfAndDisallowedOrigin_AreRejectedWithoutWrites(string operation)
    {
        using var client=Factory.CreateClient(new() {BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        var token=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var signup=new HttpRequestMessage(HttpMethod.Post,"/api/auth/sign-up")
        {Content=JsonContent.Create(new SignUpRequest("B07 CSRF","b07-csrf@appetee.test","Password123!"))};
        signup.Headers.Add("Origin","https://localhost:4200"); signup.Headers.Add("X-CSRF-TOKEN",token.GetProperty("requestToken").GetString());
        using var signedIn=await client.SendAsync(signup); signedIn.EnsureSuccessStatusCode();
        var owner=(await signedIn.Content.ReadFromJsonAsync<Appetee.Application.Models.Auth.AuthResult>())!.userId;
        await SeedPlansAsync(owner,1);
        var id=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM nutrition_plans WHERE user_id=@Owner;",new{Owner=owner});
        var before=await SnapshotAsync();
        foreach(var origin in new[]{"https://localhost:4200","https://untrusted.example"})
        {
            var path=operation=="current"?Current:$"{Plans}/{id}"+(operation=="duplicate"?"/duplicate":"");
            using var request=new HttpRequestMessage(new HttpMethod(operation=="duplicate"?"POST":operation=="delete"?"DELETE":"PUT"),path);
            request.Headers.Add("Origin",origin); request.Headers.Add("X-Meal-Plan-Revision","0");
            if(operation=="update") request.Content=JsonContent.Create(Metadata("Edited"));
            if(operation=="current") request.Content=JsonContent.Create(new{nutritionPlanId=id});
            using var response=await client.SendAsync(request);
            if(origin=="https://localhost:4200") await ErrorAsync(response,HttpStatusCode.BadRequest,"csrf_invalid");
            else { Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore); }
            Assert.Equal(before,await SnapshotAsync());
        }
    }

    [Fact]
    public async Task CurrentAndDuplicateFailure_RollBackFirstSettingsAndNewPlan()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            await SeedPlansAsync(auth.userId,1);
            var id=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM nutrition_plans WHERE user_id=@Owner;",new{Owner=auth.userId});
            var before=await SnapshotAsync();
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b07_fail BEFORE UPDATE ON meal_plan_user_settings FOR EACH ROW BEGIN IF NEW.revision>OLD.revision THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic failure'; END IF; END;");
            try
            {
                foreach(var operation in new[]{"current","duplicate"})
                {
                    using var response=await OperationAsync(client,operation,id);
                    Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode); Assert.Equal(before,await SnapshotAsync());
                }
            }
            finally { await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b07_fail;"); }
        }
    }

    [Fact]
    public async Task MetadataDuplicateAndCurrent_NeverInvokeCalculator()
    {
        var (normal,_)=await CreateAuthenticatedClientAsync(); int id;
        using(normal) id=(await CreateAsync(normal,Intermediate())).Plan!.Id;
        using var customized=Factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>
        { s.RemoveAll<INutritionCalculationService>(); s.AddSingleton<INutritionCalculationService>(new ForbiddenCalculator()); }));
        using var client=AppeteeWebApplicationFactory.CreateCsrfClient(customized);
        // Use a second synthetic authenticated owner with a seeded complete plan;
        // do not transfer session cookies between client implementations.
        using var signup=await client.PostAsJsonAsync("/api/auth/sign-up",new SignUpRequest("B07 pure","b07-pure@appetee.test","Password123!")); signup.EnsureSuccessStatusCode();
        var owner=(await signup.Content.ReadFromJsonAsync<Appetee.Application.Models.Auth.AuthResult>())!.userId;
        await Factory.Database.ExecuteAsync("""
            INSERT INTO nutrition_plans(user_id,name,mode,default_meals_per_day,goal,activity_level,calculation_sex,birth_year,age_years,
              height_cm,weight_kg,calculation_policy_version,rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g,calculated_at,created_at,updated_at)
            SELECT @Owner,'Stored',mode,default_meals_per_day,goal,activity_level,calculation_sex,birth_year,age_years,
              height_cm,weight_kg,calculation_policy_version,rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g,calculated_at,created_at,updated_at
            FROM nutrition_plans WHERE id=@Source;
            """,new{Owner=owner,Source=id});
        var owned=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM nutrition_plans WHERE user_id=@Owner;",new{Owner=owner});
        using var update=await SendAsync(client,"PUT",$"{Plans}/{owned}",Metadata("Metadata only")); await ReceiptAsync(update);
        using var copy=await SendAsync(client,"POST",$"{Plans}/{owned}/duplicate",revision:"1"); await ReceiptAsync(copy,HttpStatusCode.Created);
        using var select=await SendAsync(client,"PUT",Current+"/",new{nutritionPlanId=owned},"2"); await ReceiptAsync(select);
    }
    private sealed class ForbiddenCalculator:INutritionCalculationService
    { public NutritionCalculationDto Calculate(NutritionCalculationRequest request,CancellationToken ct=default)=>throw new InvalidOperationException("Unexpected calculator invocation."); }

    [Fact]
    public async Task YearDriftWhileRecalculationWaitsForOwnerLock_Is409AndLeavesOriginalCalculationIntact()
    {
        var clock=new AdjustableClock(new DateTimeOffset(2029,12,31,23,59,59,TimeSpan.Zero));
        var calculator=new ObservedCalculator(clock);
        using var customized=Factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>
        {
            s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock);
            s.RemoveAll<INutritionCalculationService>(); s.AddSingleton<INutritionCalculationService>(calculator);
        }));
        using var client=AppeteeWebApplicationFactory.CreateCsrfClient(customized);
        using var signup=await client.PostAsJsonAsync("/api/auth/sign-up",new SignUpRequest("B07 year","b07-year@appetee.test","Password123!")); signup.EnsureSuccessStatusCode();
        var owner=(await signup.Content.ReadFromJsonAsync<Appetee.Application.Models.Auth.AuthResult>())!.userId;
        var calculation=new NutritionCalculationRequest(new("male",2001,180m,80m),"maintain","active");
        var source=await CreateAsync(client,new{name="Year",mode="intermediate",defaultMealsPerDay=3,calculation,reviewedPolicyVersion=NutritionPolicy.Version,reviewedCalculationYear=2029});
        calculator.Calculated=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var before=await SnapshotAsync(); using var scope=customized.Services.CreateScope();
        var locked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder=scope.ServiceProvider.GetRequiredService<MealPlanTransaction>().ExecuteAsync("test.b07.year-lock",owner,1,async _=>
        { locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); return MealPlanMutation<string>.UnchangedResult("held"); },CancellationToken.None);
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage>? pending=null;
        try
        {
            pending=SendAsync(client,"PUT",$"{Plans}/{source.Plan!.Id}",new{name="Year",defaultMealsPerDay=3,calculation,reviewedPolicyVersion=NutritionPolicy.Version,reviewedCalculationYear=2029},"1");
            await calculator.Calculated.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Now=clock.Now.AddSeconds(2); release.TrySetResult(); await holder;
            using var response=await pending; await ErrorAsync(response,HttpStatusCode.Conflict,"calculation_preview_stale"); Assert.Equal(before,await SnapshotAsync());
        }
        finally { release.TrySetResult(); await holder; if(pending is not null) (await pending).Dispose(); }
    }
    private sealed class AdjustableClock(DateTimeOffset now):TimeProvider
    { public DateTimeOffset Now {get;set;}=now; public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class ObservedCalculator(TimeProvider clock):INutritionCalculationService
    {
        public TaskCompletionSource Calculated {get;set;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public NutritionCalculationDto Calculate(NutritionCalculationRequest request,CancellationToken ct=default)
        { var result=new NutritionCalculationService(clock).Calculate(request,ct); Calculated.TrySetResult(); return result; }
    }

    [Fact]
    public async Task Explain_ScopedLifecyclePredicatesUseExistingOwnerAndPrimaryIndexes()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            var first=await CreateAsync(client,Easy());
            await CreateAsync(client,Easy("Other"),"1");
            foreach(var sql in new[]{NutritionPlanSql.Detail,NutritionPlanSql.CurrentId,NutritionPlanSql.NameExistsOther,NutritionPlanSql.Count,NutritionPlanSql.UpdateMetadata,NutritionPlanSql.Delete,NutritionPlanSql.ClearCurrent,NutritionPlanSql.SetCurrent})
            {
                var rows=await Factory.Database.QueryAsync<ExplainRow>("EXPLAIN "+sql,new{CurrentUserId=auth.userId,PlanId=first.Plan!.Id,Name="Other",DefaultMealsPerDay=4,TimestampUtc=DateTime.UtcNow});
                Assert.NotEmpty(rows); Assert.Contains(rows,r=>r.Key is not null);
            }
        }
    }
    private sealed record ExplainRow { public string? Key {get;init;} }

    [Fact]
    public async Task ConcurrentCurrentChanges_CommitOneSelectionAndNeverRewriteSavedFood()
    {
        var (client,auth)=await CreateAuthenticatedClientAsync(); using(client)
        {
            await CreateAsync(client,Easy("First"));
            var second=await CreateAsync(client,Easy("Second"),"1"); var third=await CreateAsync(client,Intermediate("Third"),"2");
            await SeedGraphAsync(auth.userId); var graph=await GraphAsync();
            var responses=await Task.WhenAll(SendAsync(client,"PUT",Current,new{nutritionPlanId=second.Plan!.Id},"3"),
                SendAsync(client,"PUT",Current,new{nutritionPlanId=third.Plan!.Id},"3"));
            try
            {
                var receipt=await ReceiptAsync(Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK));
                Assert.Equal(4,receipt.Revision); Assert.True(receipt.PlannerInvalidated); Assert.Equal(receipt.Plan!.Id,receipt.Settings.CurrentNutritionPlanId);
                await ErrorAsync(Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict),HttpStatusCode.Conflict,"meal_plan_revision_conflict");
                Assert.Equal(graph,await GraphAsync());
                using var noop=await SendAsync(client,"PUT",Current,new{nutritionPlanId=receipt.Plan.Id},"4");
                var unchanged=await ReceiptAsync(noop); Assert.Equal(4,unchanged.Revision); Assert.False(unchanged.PlannerInvalidated);
            }
            finally { foreach(var response in responses) response.Dispose(); }
        }
    }

    private Task SeedPlansAsync(int owner,int count)=>Factory.Database.ExecuteAsync(
        "INSERT INTO nutrition_plans(user_id,name,mode,default_meals_per_day,created_at,updated_at) VALUES "+
        string.Join(",",Enumerable.Range(1,count).Select(i=>$"(@Owner,'Seed {i}','Easy',3,'2026-01-01','2026-01-01')"))+";",new{Owner=owner});
    private Task SeedGraphAsync(int owner)=>Factory.Database.ExecuteAsync("""
        INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at) VALUES(@Owner,'2026-01-01','2026-01-01','2026-01-01');
        INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at)
        SELECT id,'Saved meal',1,'2026-01-01','2026-01-01' FROM meal_plan_days WHERE user_id=@Owner;
        INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
        SELECT m.id,'Recipe',1,2,1,'2026-01-01','2026-01-01' FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Owner;
        INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,sort_order,created_at,updated_at)
        SELECT m.id,'Manual',2,'2026-01-01','2026-01-01' FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Owner;
        INSERT INTO meal_plan_manual_items(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)
        SELECT i.id,'Saved manual',100,10,10,10 FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id
        JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Owner AND i.item_type='Manual';
        """,new{Owner=owner});
    private Task<string?> CalculationSnapshotAsync(int id)=>Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_ARRAY(goal,activity_level,calculation_sex,birth_year,age_years,height_cm,weight_kg,
            calculation_policy_version,rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g,calculated_at) AS CHAR)
        FROM nutrition_plans WHERE id=@Id;
        """,new{Id=id});
    private Task<string?> ProfileAsync(int id)=>Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_ARRAY(calculation_sex,birth_year,height_cm,weight_kg,updated_at) AS CHAR) FROM users WHERE id=@Id;
        """,new{Id=id});
    private Task<string?> GraphAsync()=>Factory.Database.QuerySingleOrDefaultAsync<string>(GraphSql);
    private const string GraphSql="""
        SELECT CAST(JSON_OBJECT(
          'days',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,user_id,plan_date,created_at,updated_at)) FROM meal_plan_days),
          'meals',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,meal_plan_day_id,name,sort_order,created_at,updated_at)) FROM meal_plan_day_meals),
          'items',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)) FROM meal_plan_meal_items),
          'manual',(SELECT JSON_ARRAYAGG(JSON_ARRAY(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)) FROM meal_plan_manual_items)) AS CHAR);
        """;
    private async Task<string> SnapshotAsync()
    {
        var roots=await Factory.Database.QuerySingleOrDefaultAsync<string>("""
            SELECT CAST(JSON_OBJECT(
              'users',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,calculation_sex,birth_year,height_cm,weight_kg,updated_at)) FROM users),
              'settings',(SELECT JSON_ARRAYAGG(JSON_ARRAY(user_id,current_nutrition_plan_id,revision,created_at,updated_at)) FROM meal_plan_user_settings),
              'plans',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,user_id,name,mode,default_meals_per_day,goal,activity_level,calculation_sex,birth_year,
                age_years,height_cm,weight_kg,calculation_policy_version,rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,
                target_carbs_g,target_fat_g,calculated_at,created_at,updated_at)) FROM nutrition_plans)) AS CHAR);
            """);
        return roots+"\n"+await GraphAsync();
    }
    private static async Task ErrorAsync(HttpResponseMessage response,HttpStatusCode status,string code)
    {
        var text=await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode==status,$"Expected {status}, got {response.StatusCode}: {text}");
        using var json=JsonDocument.Parse(text); Assert.Equal(code,json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId",out _)); Assert.True(response.Headers.CacheControl!.NoStore);
    }
}
