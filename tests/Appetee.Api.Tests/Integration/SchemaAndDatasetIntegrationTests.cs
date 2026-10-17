using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using MySqlConnector;
using Xunit.Abstractions;

namespace Appetee.Api.Tests.Integration;

// Explicit database opt-in and loopback-only appetee_backend_tests guard live in ApiTestDatabase.
// The shared test database is reset before each test; production/appetee are never touched.
public sealed class SchemaAndDatasetIntegrationTests(
    AppeteeWebApplicationFactory factory, ITestOutputHelper output) : IntegrationTestBase(factory)
{
    private const string Repair = "migrations/20261016_011_repair_missing_application_tables.sql";

    [Fact]
    public async Task MissingTablesAreRepairedWithoutChangingCatalogueAndPlannerReadsSucceed()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var recipes = await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipes;");
            await Factory.Database.ExecuteAsync("DROP TABLE meal_plan_manual_items; DROP TABLE account_permissions; DROP TABLE account_closure_cleanup;");
            await Factory.Database.ExecuteMigrationAsync(Repair);
            Assert.Equal(recipes, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipes;"));
            Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_permissions;"));
            Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_closure_cleanup;"));
            var week = await client.GetFromJsonAsync<MealPlannerRangeDto>("/api/meal-plan/days?from=2026-10-12&through=2026-10-18");
            Assert.Equal(7, week!.Days.Count);
            Assert.Equal("0", week.Revision);
            Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='appetee_schema_migrations';"));
        }
    }

    [Fact]
    public async Task RepeatRepairPreservesExistingCleanupWorkAndPermissionGrants()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            await Factory.Database.ExecuteAsync("""
                INSERT INTO account_permissions(user_id,permission,granted_at) VALUES (@Id,'Catalog.ReadAdmin',UTC_TIMESTAMP(6));
                INSERT INTO account_closure_cleanup(former_user_id,status) VALUES (@Id,'pending');
                """, new { Id = user.userId });
            await Factory.Database.ExecuteMigrationAsync(Repair);
            await Factory.Database.ExecuteMigrationAsync(Repair);
            Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_permissions;"));
            Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_closure_cleanup WHERE status='pending';"));
        }
    }

    [Fact]
    public async Task IncompatibleColumnsAreRejectedBeforeAnyMissingTableIsCreated()
    {
        await Factory.Database.ExecuteAsync("""
            DROP TABLE meal_plan_manual_items;
            CREATE TABLE meal_plan_manual_items (meal_plan_meal_item_id VARCHAR(50) PRIMARY KEY);
            DROP TABLE account_permissions;
            """);
        var error = await Assert.ThrowsAsync<MySqlException>(() => Factory.Database.ExecuteMigrationAsync(Repair));
        Assert.Equal(1644, error.Number);
        Assert.Contains("incompatible manual item columns", error.Message);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='account_permissions';"));
    }

    [Fact]
    public async Task MissingPrerequisitesAreRejectedBeforeRepair()
    {
        await Factory.Database.ExecuteAsync("DROP TABLE administrative_audit; DROP TABLE meal_plan_manual_items;");
        var error = await Assert.ThrowsAsync<MySqlException>(() => Factory.Database.ExecuteMigrationAsync(Repair));
        Assert.Equal(1644, error.Number);
        Assert.Contains("required parent/application tables", error.Message);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='meal_plan_manual_items';"));
    }

    [Fact]
    public async Task MissingPermissionConstraintIsRejectedBeforeRepair()
    {
        await Factory.Database.ExecuteAsync("ALTER TABLE account_permissions DROP CHECK chk_account_permissions_code; DROP TABLE meal_plan_manual_items;");
        var error = await Assert.ThrowsAsync<MySqlException>(() => Factory.Database.ExecuteMigrationAsync(Repair));
        Assert.Equal(1644, error.Number);
        Assert.Contains("incompatible account permission constraints", error.Message);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='meal_plan_manual_items';"));
    }

    [Fact]
    public async Task AllGeneratedSqlLoadsOnLocalMysqlAndMatchesCanonicalData()
    {
        var environment = await Factory.Database.ReadEnvironmentAsync();
        Assert.Equal("appetee_backend_tests", environment!.DatabaseName);
        output.WriteLine($"Disposable MySQL: version={environment.Version}; sql_mode={environment.SqlMode}; max_allowed_packet={environment.MaxAllowedPacket}");
        await Factory.Database.ImportGeneratedDatasetAsync();
        Assert.Equal(121, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM ingredients;"));
        Assert.Equal(121, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM ingredient_nutrition;"));
        Assert.Equal(1699, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipes;"));
        Assert.Equal(8, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM diets;"));
        Assert.Equal(9, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(DISTINCT badge) FROM recipe_badges;"));

        using var stream = typeof(Appetee.DatabaseMigrator.MigrationRunner).Assembly.GetManifestResourceStream("Baseline/01-schema.sql")!;
        using var reader = new StreamReader(stream);
        var schema = await reader.ReadToEndAsync();
        var expectedTables = Regex.Matches(schema, @"CREATE TABLE(?: IF NOT EXISTS)? (\w+)").Select(m => m.Groups[1].Value).Order().ToArray();
        var actualTables = await Factory.Database.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='BASE TABLE' ORDER BY table_name;");
        // The reset owns canonical tables, not unrelated/legacy tables already in the disposable DB.
        Assert.Empty(expectedTables.Except(actualTables));
        await Factory.Database.ExecuteMigrationAsync(Repair);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Appetee.sln"))) root = root.Parent;
        Assert.NotNull(root);
        using var index = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, "data", "recipes", "index.json")));
        var expectedRecipes = index.RootElement.EnumerateArray().ToArray();
        var rows = await Factory.Database.QueryAsync<RecipeSeedRow>(
            "SELECT name Name, image_blob_name MainImage, card_image_blob_name CardImage, estimated_cost_per_serving Cost FROM recipes;");
        var byName = rows.ToDictionary(row => row.Name);
        foreach (var expected in expectedRecipes)
        {
            var row = byName[expected.GetProperty("name").GetString()!];
            var seed = expected.GetProperty("seedId").GetString();
            Assert.Equal($"dataset/recipes/{seed}/main.avif", row.MainImage);
            Assert.Equal($"dataset/recipes/{seed}/card.avif", row.CardImage);
            Assert.Equal(decimal.Round(expected.GetProperty("costPerServingUsd").GetDecimal(), 2, MidpointRounding.AwayFromZero), row.Cost);
        }
        Assert.Equal(expectedRecipes.Sum(r => r.GetProperty("ingredientSeedIds").GetArrayLength()),
            await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipe_ingredients;"));
        Assert.Equal(expectedRecipes.Sum(r => r.GetProperty("diets").GetArrayLength()),
            await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM diet_recipes;"));
        Assert.Equal(expectedRecipes.Sum(r => r.GetProperty("badges").GetArrayLength()),
            await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipe_badges;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*) FROM recipe_ingredients ri
            LEFT JOIN recipes r ON r.id=ri.recipe_id LEFT JOIN ingredients i ON i.id=ri.ingredient_id
            WHERE r.id IS NULL OR i.id IS NULL;
            """));
    }

    public sealed record RecipeSeedRow(string Name, string MainImage, string CardImage, decimal Cost);
}
