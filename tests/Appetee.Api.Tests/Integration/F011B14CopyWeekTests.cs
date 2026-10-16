using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using System.Net;
using System.Text.Json;
using Appetee.Application.Requests.MealPlans;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.MealPlans;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Dapper;
using System.Data;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B14CopyWeekTests(AppeteeWebApplicationFactory factory):PlannerCopyTestBase(factory)
{
    [Fact]
    public async Task GappedAutoIncrementIds_MapEveryParentAndManualDetailByPosition()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);using(client)
        {
            for(var offset=0;offset<7;offset++)await SeedDay(user.userId,new DateOnly(2026,10,12).AddDays(offset).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture));
            var source=(await Read(client,Source,"2026-10-18"))!;
            var transaction=new MealPlanTransaction(new GappedConnectionFactory(Factory.Database.ConnectionString),TimeProvider.System,NullLogger<MealPlanTransaction>.Instance);
            var service=new Appetee.Application.Services.MealPlans.PlannerCopyService(new PlannerCopyRepository(transaction));
            var receipt=await service.CopyWeekAsync(user.userId,new CopyWeekRequest {SourceMonday=Source,DestinationMonday=Destination,ReplaceExisting=false},0,default);
            for(var offset=0;offset<7;offset++)Assert.Equal(Shape(source.Days[offset]),Shape(receipt.Days[offset]));
            var ids=receipt.Days.SelectMany(d=>d.Meals).SelectMany(m=>m.Items).Select(i=>i.Id).Order().ToArray();
            Assert.All(ids.Zip(ids.Skip(1)),pair=>Assert.True(pair.Second-pair.First>=7));
            Assert.Equal(7,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_manual_items x JOIN meal_plan_meal_items i ON i.id=x.meal_plan_meal_item_id JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id AND d.plan_date BETWEEN '2026-10-19' AND '2026-10-25';",new {Id=user.userId}));
        }
    }

    [Fact]
    public async Task SevenOffsetsMixAbsentEmptyPopulatedAndEmptyMeals_OneRevisionAndExactReplacement()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            for(var offset=0;offset<7;offset++)
            {
                var src=new DateOnly(2026,10,12).AddDays(offset).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
                var dst=new DateOnly(2026,10,19).AddDays(offset).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
                if(offset%3!=0)await SeedDay(user.userId,src,offset%3==1 ? 0 : 3);
                await SeedDay(user.userId,dst);
            }
            var source=(await Read(client,Source,"2026-10-18"))!;var original=JsonSerializer.Serialize(source.Days);var before=await Snapshot(user.userId);
            using var unconfirmed=await CopyWeek(client,replace:false);await Code(unconfirmed,"destination_not_empty");Assert.Equal(before,await Snapshot(user.userId));
            using var copy=await CopyWeek(client);var receipt=await Receipt(copy);Assert.Equal("1",receipt.Revision);Assert.Equal(7,receipt.Days.Count);
            for(var offset=0;offset<7;offset++){Assert.Equal(new DateOnly(2026,10,19).AddDays(offset),receipt.Days[offset].Date);Assert.Equal(Shape(source.Days[offset]),Shape(receipt.Days[offset]));}
            Assert.Equal(original,JsonSerializer.Serialize((await Read(client,Source,"2026-10-18"))!.Days));
        }
    }

    [Fact]
    public async Task FourthDateFault_RestoresAllSevenDestinationsAndRevision()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            for(var offset=0;offset<7;offset++)
            {await SeedDay(user.userId,new DateOnly(2026,10,12).AddDays(offset).ToString("yyyy-MM-dd"));await SeedDay(user.userId,new DateOnly(2026,10,19).AddDays(offset).ToString("yyyy-MM-dd"));}
            var before=await Snapshot(user.userId);
            await Factory.Database.ExecuteAsync("CREATE TRIGGER b14_fault BEFORE INSERT ON meal_plan_days FOR EACH ROW BEGIN IF NEW.plan_date='2026-10-22' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic fourth-day fault'; END IF; END;");
            try{using var copy=await CopyWeek(client);Assert.Equal(HttpStatusCode.InternalServerError,copy.StatusCode);Assert.Equal(before,await Snapshot(user.userId));}
            finally{await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS b14_fault;");}
        }
    }

    [Theory]
    [InlineData("2026-10-13","2026-10-19")][InlineData("2026-10-12","2026-10-20")]
    [InlineData("2026-10-12","2026-10-12")][InlineData("9999-12-27","2026-10-19")]
    [InlineData("2026-10-12","9999-12-27")][InlineData("0999-12-30","2026-10-19")]
    public async Task NonMondaySameWeekAndOverflow_RejectWithoutSettings(string source,string destination)
    {
        var(client,user)=await CreateAuthenticatedClientAsync();using(client)
        {using var copy=await CopyWeek(client,source,destination);Assert.Equal(HttpStatusCode.BadRequest,copy.StatusCode);await Code(copy,"validation_failed");Assert.Equal("",await Snapshot(user.userId));}
    }

    [Fact]
    public async Task DestinationChangeAfterReviewAndConcurrentCopies_ConflictWithoutPartialReplacement()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);await SeedDay(user.userId,Destination);
            var day=(await Read(client,Destination))!.Days[0];using var rename=await Send(client,HttpMethod.Patch,$"/api/meal-plan/meals/{day.Meals[0].Id}",new {name="Destination changed"},"0");await Receipt(rename);
            var before=await Snapshot(user.userId);using var stale=await CopyWeek(client);await Code(stale,"meal_plan_revision_conflict");Assert.Equal(before,await Snapshot(user.userId));
            var responses=await Task.WhenAll(CopyWeek(client,revision:"1"),CopyWeek(client,revision:"1"));
            try{Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);await Code(responses.Single(r=>r.StatusCode==HttpStatusCode.Conflict),"meal_plan_revision_conflict");}
            finally{foreach(var response in responses)response.Dispose();}
            Assert.Equal("2",(await Read(client,Destination,"2026-10-25"))!.Revision);
        }
    }
    private sealed class GappedConnectionFactory(string connectionString):IDbConnectionFactory
    {
        public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct)
        {
            if(new MySqlConnectionStringBuilder(connectionString).Database!="appetee_backend_tests")throw new InvalidOperationException("Isolated database required.");
            var connection=new MySqlConnection(connectionString);
            try{await connection.OpenAsync(ct);await connection.ExecuteAsync(new CommandDefinition("SET SESSION auto_increment_increment=7,auto_increment_offset=1;",cancellationToken:ct));return connection;}
            catch{await connection.DisposeAsync();throw;}
        }
    }
}
