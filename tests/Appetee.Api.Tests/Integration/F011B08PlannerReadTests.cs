using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Infrastructure.MealPlans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Appetee.Application.Services.MealPlans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Dapper;
using System.Data;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B08PlannerReadTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UntouchedWeek_UsesRevisionZeroAndWritesNothing()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            using var scope=Factory.Services.CreateScope();
            var direct=await scope.ServiceProvider.GetRequiredService<IMealPlannerService>().GetRangeAsync(user.userId,"1000-01-01","1000-01-07",default);
            Assert.Equal(7,direct.Days.Count);
            var dto=await client.GetFromJsonAsync<MealPlannerRangeDto>("/api/meal-plan/days?from=1000-01-01&through=1000-01-07");
            Assert.Equal("0",dto!.Revision); Assert.Null(dto.Settings.CurrentPlan); Assert.Equal(7,dto.Days.Count);
            Assert.All(dto.Days,d=>{Assert.Equal("untouched",d.Persistence);Assert.Null(d.Id);Assert.Empty(d.Meals);});
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_user_settings;"));
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days;"));
        }
    }
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)]
    public async Task EasyCurrent_ProjectsDefaultsButSavedEmptyAndForeignDayRemainDistinct(int count)
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        var (foreign,other)=await CreateAuthenticatedClientAsync();
        using(client)using(foreign)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Meal-Plan-Revision","0");
            using var create=await client.PostAsJsonAsync("/api/nutrition-plans",new { name="Synthetic defaults",mode="easy",defaultMealsPerDay=count });
            create.EnsureSuccessStatusCode();
            await Factory.Database.ExecuteAsync("""
                INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at) VALUES(@Id,'2026-10-12',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),(@Other,'2026-10-13',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                """,new { Id=user.userId,Other=other.userId });
            var dto=await client.GetFromJsonAsync<MealPlannerRangeDto>("/api/meal-plan/days?from=2026-10-12&through=2026-10-13");
            Assert.Equal("1",dto!.Revision); Assert.Empty(dto.Days[0].Meals); Assert.Equal("saved",dto.Days[0].Persistence);
            Assert.Equal("untouched",dto.Days[1].Persistence); Assert.Null(dto.Days[1].Id); Assert.Equal(count,dto.Days[1].Meals.Count);
            Assert.Equal(0,dto.Days[1].SavedMealCount); Assert.Null(dto.Days[1].Targets); Assert.Null(dto.Days[1].Remaining);
            Assert.Equal(2,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days;"));
        }
    }
    [Theory]
    [InlineData("from=2026-10-12&through=2026-10-19")]
    [InlineData("from=2026-10-13&through=2026-10-12")]
    [InlineData("from=2026-02-30&through=2026-03-01")]
    [InlineData("from=2026-10-12")]
    [InlineData("from=2026-10-12&through=2026-10-12&userId=1")]
    [InlineData("from=2026-10-12&from=2026-10-13&through=2026-10-13")]
    public async Task Range_RejectsInvalidOrOverpostedParameters(string query)
    {
        var (client,_)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            using var response=await client.GetAsync("/api/meal-plan/days?"+query);
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days;"));
        }
    }
    [Fact]
    public async Task SavedRecipe_RemainsVisibleWhenIncompatible_AndUnknownFatNeverBecomesZero()
    {
        var (client,user)=await CreateAuthenticatedClientAsync(dietIds:[],ingredientRestrictionIds:[]);
        using(client)
        {
            await Factory.Database.ExecuteAsync("""
                UPDATE recipes SET calories_total=100,protein_total=10,carbs_total=0,fat_total=NULL,servings=3 WHERE id=1;
                INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at) VALUES(@Id,'2026-10-12',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                SET @Day=LAST_INSERT_ID();
                INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at) VALUES(@Day,'Synthetic meal',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                SET @Meal=LAST_INSERT_ID();
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
                VALUES(@Meal,'Recipe',1,3,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                """,new { Id=user.userId });
            const string route="/api/meal-plan/days?from=2026-10-12&through=2026-10-12";
            var available=await client.GetFromJsonAsync<MealPlannerRangeDto>(route);
            Assert.Equal(99.99m,available!.Days[0].Totals.CaloriesKcal); Assert.Null(available.Days[0].Totals.FatG);
            Assert.Equal(["recipe_fat_unknown"],available.Days[0].Warnings);
            await Factory.Database.ExecuteAsync("INSERT INTO user_ingredient_restrictions(user_id,ingredient_id) VALUES(@Id,1);",new { Id=user.userId });
            var unavailable=await client.GetFromJsonAsync<MealPlannerRangeDto>(route);
            var recipe=Assert.IsType<PlannerRecipeItemDto>(Assert.Single(unavailable!.Days[0].Meals[0].Items));
            Assert.Equal("unavailable",recipe.Availability);Assert.Null(recipe.Name);Assert.Null(recipe.Nutrition.CaloriesKcal);
            Assert.Equal(["recipe_unavailable"],unavailable.Days[0].Warnings); Assert.Equal("0",unavailable.Revision);
        }
    }
    [Fact]
    public async Task RangeQuery_ContainsFourResultsAndHasAnExplainPlan()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var arguments=MealPlannerReader.Parameters(user.userId,new(2026,10,12),new(2026,10,18));
            var plan=await Factory.Database.QueryAsync<object>("EXPLAIN "+MealPlannerSql.Items,arguments);
            Assert.NotEmpty(plan);
            Assert.Equal(4,MealPlannerSql.Range.Count(c=>c==';'));
            using var response=await client.GetAsync("/api/meal-plan/days?from=9999-12-31&through=9999-12-31");
            response.EnsureSuccessStatusCode(); Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }

    [Fact]
    public async Task MaximumWeek_ExecutesOneFourResultCommand_RegardlessOfItemCount()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var counted=new CountingDbConnectionFactory(Factory.Database.ConnectionString);
            var queries=new MealPlannerQueries(counted,NullLogger<MealPlannerQueries>.Instance);
            var from=new DateOnly(2026,10,12);var through=from.AddDays(6);
            var empty=await queries.GetRangeAsync(user.userId,from,through,default);
            Assert.Empty(empty.Items);Assert.Equal(1,counted.Executions);
            var days=string.Join(" UNION ALL ",Enumerable.Range(0,7).Select(n=>$"SELECT {n} n"));
            var meals=string.Join(" UNION ALL ",Enumerable.Range(1,12).Select(n=>$"SELECT {n} n"));
            var items=string.Join(" UNION ALL ",Enumerable.Range(1,20).Select(n=>$"SELECT {n} n"));
            await Factory.Database.ExecuteAsync($"""
                INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at)
                SELECT @Id,DATE_ADD('2026-10-12',INTERVAL n.n DAY),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6) FROM ({days}) n;
                INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at)
                SELECT d.id,'Synthetic maximum meal',n.n,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6) FROM meal_plan_days d CROSS JOIN ({meals}) n WHERE d.user_id=@Id;
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
                SELECT m.id,'Manual',NULL,NULL,n.n,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6) FROM meal_plan_day_meals m
                JOIN meal_plan_days d ON d.id=m.meal_plan_day_id CROSS JOIN ({items}) n WHERE d.user_id=@Id;
                INSERT INTO meal_plan_manual_items(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)
                SELECT i.id,'Synthetic maximum entry',1,1,1,1 FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id
                JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id;
                """,new {Id=user.userId});
            var full=await queries.GetRangeAsync(user.userId,from,through,default);
            Assert.Equal(7,full.Days.Count);Assert.Equal(84,full.Meals.Count);Assert.Equal(1680,full.Items.Count);
            Assert.Equal(2,counted.Executions); // One additional read, no per-item/preview/detail queries.
            var range=MealPlanReadModelBuilder.Build(full,from,through);
            Assert.All(range.Days,day=>{Assert.Equal(12,day.Meals.Count);Assert.Equal(240m,day.Totals.CaloriesKcal);});
        }
    }

    [Fact]
    public async Task ReadSnapshot_RemainsCoherentWhenAnotherConnectionChangesCurrentAndDays()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await using var connection=new MySqlConnection(Factory.Database.ConnectionString);
            await connection.OpenAsync();await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            using var grid=await connection.QueryMultipleAsync(new CommandDefinition(MealPlannerSql.Range,
                MealPlannerReader.Parameters(user.userId,new(2026,10,12),new(2026,10,12)),transaction));
            var context=await grid.ReadSingleAsync<Appetee.Application.RowData.MealPlans.PlannerContextRowData>();
            Assert.Equal(0,context.Revision);Assert.Null(context.CurrentNutritionPlanId);
            using var create=new HttpRequestMessage(HttpMethod.Post,"/api/nutrition-plans"){Content=JsonContent.Create(new {name="Concurrent current",mode="easy",defaultMealsPerDay=4})};
            create.Headers.Add("X-Meal-Plan-Revision","0");using var response=await client.SendAsync(create);response.EnsureSuccessStatusCode();
            await Factory.Database.ExecuteAsync("INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at) VALUES(@Id,'2026-10-12',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));",new {Id=user.userId});
            Assert.Empty(await grid.ReadAsync<Appetee.Application.RowData.MealPlans.PlannerDayRowData>());
            Assert.Empty(await grid.ReadAsync<Appetee.Application.RowData.MealPlans.PlannerMealRowData>());
            Assert.Empty(await grid.ReadAsync<Appetee.Application.RowData.MealPlans.PlannerItemRowData>());
            await transaction.CommitAsync();
            var fresh=(await client.GetFromJsonAsync<MealPlannerRangeDto>("/api/meal-plan/days?from=2026-10-12&through=2026-10-12"))!;
            Assert.Equal("1",fresh.Revision);Assert.NotNull(fresh.Settings.CurrentPlan);Assert.Equal("saved",fresh.Days[0].Persistence);
        }
    }

    [Fact]
    public async Task CorruptManualDetail_FailsInsteadOfReturningZero_AndGETDoesNotChangeRows()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            await Factory.Database.ExecuteAsync("""
                INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at) VALUES(@Id,'2026-10-12',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at) VALUES(LAST_INSERT_ID(),'Synthetic corrupt meal',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,sort_order,created_at,updated_at) VALUES(LAST_INSERT_ID(),'Manual',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
                """,new {Id=user.userId});
            using var response=await client.GetAsync("/api/meal-plan/days?from=2026-10-12&through=2026-10-12");
            Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_meal_items;"));
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_user_settings;"));
        }
    }

    [Fact]
    public async Task IntermediateCurrent_AppliesStoredTargetsToPastAndFutureDatesWithoutWrites()
    {
        var (client,user)=await CreateAuthenticatedClientAsync();
        using(client)
        {
            var year=Factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().Year;
            using var create=new HttpRequestMessage(HttpMethod.Post,"/api/nutrition-plans") {Content=JsonContent.Create(new {
                name="Synthetic intermediate",mode="intermediate",defaultMealsPerDay=3,
                calculation=new {profile=new {calculationSex="male",birthYear=year-30,heightCm=180m,weightKg=75m},goal="maintain",activityLevel="active"},
                reviewedPolicyVersion=Appetee.Application.Models.MealPlans.NutritionPolicy.Version,reviewedCalculationYear=year })};
            create.Headers.Add("X-Meal-Plan-Revision","0");using var created=await client.SendAsync(create);created.EnsureSuccessStatusCode();
            var saved=(await created.Content.ReadFromJsonAsync<NutritionPlanMutationDto>())!;
            Assert.NotNull(saved.Settings.CurrentPlan!.Targets);
            foreach(var date in new[]{"1970-01-01","9999-12-31"})
            {
                var range=(await client.GetFromJsonAsync<MealPlannerRangeDto>($"/api/meal-plan/days?from={date}&through={date}"))!;
                Assert.Equal(saved.Settings.CurrentPlan.Targets,range.Days[0].Targets);
                Assert.Equal(range.Days[0].Targets,range.Days[0].Remaining);
                Assert.Equal("1",range.Revision);Assert.Equal(3,range.Days[0].Meals.Count);
            }
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_days WHERE user_id=@Id;",new{Id=user.userId}));
        }
    }
}
