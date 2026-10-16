using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Services.MealPlans;
using Appetee.Infrastructure.MealPlans;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Text.Json;
using Xunit.Abstractions;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B15PerformanceTests(AppeteeWebApplicationFactory factory,ITestOutputHelper output):PlannerCopyTestBase(factory)
{
    [Fact]
    public async Task GuardedEnvironment_HasSupportedMySqlStrictModeInnoDbAndEnforcedChecks()
    {
        var environment=await Factory.Database.ReadEnvironmentAsync();Assert.NotNull(environment);
        Assert.Equal("appetee_backend_tests",environment.DatabaseName);Assert.DoesNotContain("MariaDB",environment.Version,StringComparison.OrdinalIgnoreCase);
        var version=Version.Parse(environment.Version.Split('-')[0]);Assert.True(version>=new Version(8,0,16));
        Assert.True(environment.SqlMode.Contains("STRICT_TRANS_TABLES",StringComparison.Ordinal) || environment.SqlMode.Contains("STRICT_ALL_TABLES",StringComparison.Ordinal));
        var engines=await Factory.Database.QueryAsync<string>("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('nutrition_plans','meal_plan_user_settings','meal_plan_days','meal_plan_day_meals','meal_plan_meal_items','meal_plan_manual_items');");
        Assert.Equal(6,engines.Count);Assert.All(engines,e=>Assert.Equal("InnoDB",e));
        var checks=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_TYPE='CHECK' AND TABLE_NAME LIKE 'meal_plan_%';");Assert.True(checks>=6);
        output.WriteLine($"Isolated database={environment.DatabaseName}; MySQL={environment.Version}; SQL mode={environment.SqlMode}; max_allowed_packet={environment.MaxAllowedPacket}; no production identity/migration claim.");
    }

    [Fact]
    public async Task MaximumMixedWeek_CopyIsBatchedAndRangeUsesOneFourResultCommand()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);using var small=await CopyWeek(client);await Receipt(small);
            var ordinary=Assert.Single(Factory.MealPlanDiagnostics.Operations,o=>o.Operation=="copy.week");
            await Factory.Database.ExecuteAsync("DELETE FROM meal_plan_days WHERE user_id=@Id;",new {Id=user.userId});
            await SeedMaximum(user.userId);
            var counted=new CountingDbConnectionFactory(Factory.Database.ConnectionString);
            var queries=new MealPlannerQueries(counted,NullLogger<MealPlannerQueries>.Instance);var clock=Stopwatch.StartNew();
            var rows=await queries.GetRangeAsync(user.userId,new(2026,10,12),new(2026,10,18),default);
            var source=MealPlanReadModelBuilder.Build(rows,new(2026,10,12),new(2026,10,18));clock.Stop();
            Assert.Equal(1,counted.Executions);Assert.Equal(7,rows.Days.Count);Assert.Equal(84,rows.Meals.Count);Assert.Equal(1680,rows.Items.Count);
            Assert.Equal(840,rows.Items.Count(i=>i.ItemType=="Manual"));Assert.All(source.Days,d=>{Assert.Null(d.Totals.FatG);Assert.Contains("recipe_fat_unknown",d.Warnings);});
            var payload=JsonSerializer.SerializeToUtf8Bytes(source).Length;
            Factory.MealPlanDiagnostics.Clear();var copyClock=Stopwatch.StartNew();using var response=await CopyWeek(client,revision:"1");var copied=await Receipt(response);copyClock.Stop();
            Assert.Equal("2",copied.Revision);Assert.Equal(1680,copied.Days.SelectMany(d=>d.Meals).Sum(m=>m.Items.Count));
            for(var offset=0;offset<7;offset++)Assert.Equal(Shape(source.Days[offset]),Shape(copied.Days[offset]));
            var maximum=Assert.Single(Factory.MealPlanDiagnostics.Operations,o=>o.Operation=="copy.week");
            var environment=(await Factory.Database.ReadEnvironmentAsync())!;
            var batch=(int)Math.Clamp(environment.MaxAllowedPacket/4096,1,PlannerCopySql.MaximumBatchRows);
            var extraBatches=(int)Math.Ceiling(1680d/batch)-1+(int)Math.Ceiling(840d/batch)-1;
            Assert.True(maximum.SqlCommands<=ordinary.SqlCommands+extraBatches,$"ordinary={ordinary.SqlCommands}; maximum={maximum.SqlCommands}; batch={batch}");
            Assert.Equal(7+84+1680+840,maximum.ReportedRows);
            var after=await queries.GetRangeAsync(user.userId,new(2026,10,19),new(2026,10,25),default);Assert.Equal(2,counted.Executions);Assert.Equal(1680,after.Items.Count);
            var timestamps=await Factory.Database.QueryAsync<DateTime>("""
                SELECT created_at FROM meal_plan_days WHERE user_id=@Id AND plan_date BETWEEN '2026-10-19' AND '2026-10-25'
                UNION SELECT m.created_at FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id AND d.plan_date BETWEEN '2026-10-19' AND '2026-10-25'
                UNION SELECT i.created_at FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id AND d.plan_date BETWEEN '2026-10-19' AND '2026-10-25';
                """,new {Id=user.userId});Assert.Single(timestamps);
            output.WriteLine($"Read commands=1, result sets=4, rows=7/84/1680, elapsedMs={clock.ElapsedMilliseconds}, payloadBytes={payload}; copy ordinaryCommands={ordinary.SqlCommands}, maxCommands={maximum.SqlCommands}, batchRows={batch}, affectedRows={maximum.ReportedRows}, transactionMs={maximum.ElapsedMs}, receiptMs={copyClock.ElapsedMilliseconds}, packet={environment.MaxAllowedPacket}.");
        }
    }

    [Fact]
    public async Task CopyAccessPaths_AreOwnerDateParentIndexed()
    {
        var(client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await SeedDay(user.userId,Source);var parameters=new {CurrentUserId=user.userId,From=Source,Through="2026-10-18",RecipeIds=new[]{1}};
            foreach(var sql in new[]{MealPlannerSql.Days,MealPlannerSql.Meals,PlannerCopySql.ItemMapping,PlannerCopySql.LockRecipes})
            {
                var rows=await Factory.Database.QueryAsync<Plan>("EXPLAIN "+sql,parameters);Assert.NotEmpty(rows);
                Assert.Contains(rows,r=>r.key is not null);Assert.All(rows.Where(r=>r.table is not null && !r.table.StartsWith('<')),r=>Assert.NotNull(r.key));
                output.WriteLine(string.Join("; ",rows.Select(r=>$"table={r.table},key={r.key}")));
            }
        }
    }

    private async Task SeedMaximum(int userId)
    {
        var recipeIds=await Factory.Database.QueryAsync<int>("SELECT id FROM recipes ORDER BY id LIMIT 10;");
        for(var index=recipeIds.Count;index<10;index++)
            await Factory.Database.ExecuteAsync("""
                INSERT INTO recipes(name,description,instructions,prep_time_minutes,cook_time_minutes,total_time_minutes,servings,difficulty,
                    estimated_cost_per_serving,calories_total,protein_total,carbs_total,calories_per_serving,protein_per_serving,fat_total)
                SELECT 'Synthetic maximum recipe',description,instructions,prep_time_minutes,cook_time_minutes,total_time_minutes,servings,difficulty,
                    estimated_cost_per_serving,calories_total,protein_total,carbs_total,calories_per_serving,protein_per_serving,NULL FROM recipes WHERE id=1;
                """);
        recipeIds=await Factory.Database.QueryAsync<int>("SELECT id FROM recipes ORDER BY id LIMIT 10;");
        var days=Numbers(0,7);var meals=Numbers(1,12);var items=Numbers(1,20);
        var recipeCase="CASE n.n "+string.Join(' ',recipeIds.Select((id,index)=>$"WHEN {index+1} THEN {id}"))+" ELSE NULL END";
        await Factory.Database.ExecuteAsync($"""
            UPDATE recipes SET fat_total=NULL;
            INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at)
            SELECT @Id,DATE_ADD('2026-10-12',INTERVAL n.n DAY),'2026-10-01','2026-10-01' FROM ({days}) n;
            INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at)
            SELECT d.id,CONCAT('Synthetic meal ',n.n),n.n,'2026-10-01','2026-10-01' FROM meal_plan_days d CROSS JOIN ({meals}) n WHERE d.user_id=@Id;
            INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
            SELECT m.id,IF(n.n<=10,'Recipe','Manual'),{recipeCase},IF(n.n<=10,n.n,NULL),n.n,'2026-10-01','2026-10-01'
            FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id CROSS JOIN ({items}) n WHERE d.user_id=@Id;
            INSERT INTO meal_plan_manual_items(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)
            SELECT i.id,'Synthetic maximum manual',123.45,10.01,20.02,3.03 FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id
            JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id AND i.item_type='Manual';
            """,new {Id=userId});
    }
    private static string Numbers(int first,int count)=>string.Join(" UNION ALL ",Enumerable.Range(first,count).Select(n=>$"SELECT {n} n"));
    private sealed class Plan {public string? table {get;set;}public string? key {get;set;}}
}
