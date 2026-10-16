using Appetee.Api.Tests.Infrastructure;
using Appetee.DatabaseMigrator;
using MySqlConnector;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B02SchemaTests : IntegrationTestBase
{
    private const string Timestamp = "2026-10-05 12:00:00.000000";

    public F011B02SchemaTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task ProfileAndPlanChecks_RequireAbsentOrCompleteApprovedShapes()
    {
        await AssertMySqlErrorAsync(3819, () => Factory.Database.ExecuteAsync(
            "UPDATE users SET calculation_sex = 'male' WHERE id = 1;"));

        await Factory.Database.ExecuteAsync(
            """
            UPDATE users
            SET calculation_sex = 'female', birth_year = 1995, height_cm = 168.25, weight_kg = 64.50
            WHERE id = 1;
            """);

        await AssertMySqlErrorAsync(3819, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO nutrition_plans (
                user_id, name, mode, default_meals_per_day, goal, created_at, updated_at
            ) VALUES (1, 'Invalid Easy', 'Easy', 3, 'maintain', '{Timestamp}', '{Timestamp}');
            """));

        await AssertMySqlErrorAsync(3819, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO nutrition_plans (
                user_id, name, mode, default_meals_per_day, created_at, updated_at
            ) VALUES (1, 'Incomplete Intermediate', 'Intermediate', 3, '{Timestamp}', '{Timestamp}');
            """));

        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO nutrition_plans (
                user_id, name, mode, default_meals_per_day,
                goal, activity_level, calculation_sex, birth_year, age_years, height_cm, weight_kg,
                calculation_policy_version, rmr_kcal, maintenance_kcal,
                target_calories_kcal, target_protein_g, target_carbs_g, target_fat_g,
                calculated_at, created_at, updated_at
            ) VALUES (
                1, 'Valid Intermediate', 'Intermediate', 4,
                'maintain', 'active', 'female', 1995, 31, 168.25, 64.50,
                'mp-v1.0.0-2026-10', 1500.25, 2625.44,
                2500, 104, 352, 75,
                '{Timestamp}', '{Timestamp}', '{Timestamp}'
            );
            """);

        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM nutrition_plans WHERE name = 'Valid Intermediate';"));
    }

    [Fact]
    public async Task CurrentPointer_RejectsAPlanOwnedByAnotherUser()
    {
        var userOnePlan = await InsertEasyPlanAsync(1, "User One Plan");
        var userTwoPlan = await InsertEasyPlanAsync(2, "User Two Plan");

        await AssertMySqlErrorAsync(1452, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_user_settings (
                user_id, current_nutrition_plan_id, revision, created_at, updated_at
            ) VALUES (1, {userTwoPlan}, 0, '{Timestamp}', '{Timestamp}');
            """));

        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_user_settings (
                user_id, current_nutrition_plan_id, revision, created_at, updated_at
            ) VALUES (1, {userOnePlan}, 0, '{Timestamp}', '{Timestamp}');
            """);
        Assert.Equal(userOnePlan, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT current_nutrition_plan_id FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task MealItemShapeCheck_RejectsNullRecipeQuantityAndManualRecipeId()
    {
        var (_, mealId) = await InsertDayAndMealAsync();

        await AssertMySqlErrorAsync(3819, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES ({mealId}, 'Recipe', 1, NULL, 1, '{Timestamp}', '{Timestamp}');
            """));

        await AssertMySqlErrorAsync(3819, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES ({mealId}, 'Manual', 1, NULL, 1, '{Timestamp}', '{Timestamp}');
            """));

        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_meal_items;"));
    }

    [Fact]
    public async Task SparseUniqueness_RejectsDuplicateDayAndRecipeButAllowsMultipleManualRows()
    {
        var (_, mealId) = await InsertDayAndMealAsync();

        await AssertMySqlErrorAsync(1062, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_days (user_id, plan_date, created_at, updated_at)
            VALUES (1, '2026-10-05', '{Timestamp}', '{Timestamp}');
            """));

        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES
                ({mealId}, 'Recipe', 1, 2, 1, '{Timestamp}', '{Timestamp}'),
                ({mealId}, 'Manual', NULL, NULL, 2, '{Timestamp}', '{Timestamp}'),
                ({mealId}, 'Manual', NULL, NULL, 3, '{Timestamp}', '{Timestamp}');
            """);

        await AssertMySqlErrorAsync(1062, () => Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES ({mealId}, 'Recipe', 1, 1, 4, '{Timestamp}', '{Timestamp}');
            """));

        Assert.Equal(2, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_meal_items WHERE item_type = 'Manual';"));
    }

    [Fact]
    public async Task DeleteRules_RestrictRecipeAndCurrentPlanWhileDayCascadesAllChildren()
    {
        var planId = await InsertEasyPlanAsync(1, "Current Plan");
        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_user_settings (
                user_id, current_nutrition_plan_id, revision, created_at, updated_at
            ) VALUES (1, {planId}, 0, '{Timestamp}', '{Timestamp}');
            """);
        var (dayId, mealId) = await InsertDayAndMealAsync();
        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES ({mealId}, 'Recipe', 1, 1, 1, '{Timestamp}', '{Timestamp}');
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES ({mealId}, 'Manual', NULL, NULL, 2, '{Timestamp}', '{Timestamp}');
            SET @manual_item_id = LAST_INSERT_ID();
            INSERT INTO meal_plan_manual_items (
                meal_plan_meal_item_id, name, calories_kcal, protein_g, carbs_g, fat_g
            ) VALUES (@manual_item_id, 'Manual food', 300, 20, 30, 10);
            """);

        await AssertMySqlErrorAsync(1451, () => Factory.Database.ExecuteAsync(
            "DELETE FROM recipes WHERE id = 1;"));
        await AssertMySqlErrorAsync(1451, () => Factory.Database.ExecuteAsync(
            $"DELETE FROM nutrition_plans WHERE id = {planId};"));

        await Factory.Database.ExecuteAsync(
            $"""
            UPDATE meal_plan_user_settings SET current_nutrition_plan_id = NULL WHERE user_id = 1;
            DELETE FROM nutrition_plans WHERE id = {planId};
            DELETE FROM meal_plan_days WHERE id = {dayId};
            """);

        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_day_meals;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_meal_items;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_manual_items;"));
    }

    [Fact]
    public async Task Upgrade_PreservesExistingRowsAndMigrationHistoryHandlesApplyAgain()
    {
        await Factory.Database.ExecuteAsync(
            "INSERT INTO favorite_recipes (user_id, recipe_id) VALUES (1, 1);");
        var before = await ReadIdentitySnapshotAsync();
        await RemoveB02SchemaAsync();
        // The bootstrap now includes E-002. A baseline-through-008 fixture must
        // remove every later schema slice before exercising the real upgrade.
        // This downgrade is confined to the explicitly guarded synthetic DB.
        await Factory.Database.ExecuteAsync("""
            DROP TABLE account_permissions;
            DROP TABLE administrative_audit;
            ALTER TABLE users DROP CHECK chk_users_security_version,
                DROP COLUMN is_active,DROP COLUMN security_version;
            ALTER TABLE recipes DROP CHECK chk_recipes_resource_version,
                DROP COLUMN resource_version,DROP COLUMN created_by_user_id,DROP COLUMN updated_by_user_id;
            ALTER TABLE ingredients DROP CHECK chk_ingredients_resource_version,
                DROP COLUMN resource_version,DROP COLUMN created_by_user_id,DROP COLUMN updated_by_user_id;
            """);

        var previousOptIn = Environment.GetEnvironmentVariable("APPETEE_ALLOW_MIGRATION_BASELINE");
        Environment.SetEnvironmentVariable("APPETEE_ALLOW_MIGRATION_BASELINE", "true");
        try
        {
            var output = new StringWriter();
            var runner = new MigrationRunner(Factory.Database.ConnectionString, output);
            await runner.BaselineExistingAsync(8);
            await runner.MigrateAsync();
            await runner.MigrateAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPETEE_ALLOW_MIGRATION_BASELINE", previousOptIn);
        }

        var after = await ReadIdentitySnapshotAsync();
        Assert.Equal(before, after);
        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(*) FROM appetee_schema_migrations
            WHERE migration_id = '20261004_009_f011_meal_plan_schema.sql'
              AND execution_kind = 'migration';
            """));
        Assert.Equal(6, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN (
                  'nutrition_plans', 'meal_plan_user_settings', 'meal_plan_days',
                  'meal_plan_day_meals', 'meal_plan_meal_items', 'meal_plan_manual_items'
              );
            """));
        Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*) FROM appetee_schema_migrations
            WHERE migration_id='20261012_010_e002_catalogue_permissions.sql' AND execution_kind='migration';
            """));
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_permissions;"));
    }

    [Fact]
    public async Task RangeAndCopySourceQueries_HaveValidMySqlPlansAndRequiredIndexes()
    {
        var (_, mealId) = await InsertDayAndMealAsync();
        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_meal_items (
                meal_plan_day_meal_id, item_type, recipe_id, quantity, sort_order, created_at, updated_at
            ) VALUES ({mealId}, 'Recipe', 1, 1, 1, '{Timestamp}', '{Timestamp}');
            """);

        var rangePlan = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            EXPLAIN FORMAT=JSON
            SELECT id, plan_date
            FROM meal_plan_days
            WHERE user_id = 1 AND plan_date BETWEEN '2026-10-01' AND '2026-10-07'
            ORDER BY plan_date;
            """);
        var copyPlan = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            EXPLAIN FORMAT=JSON
            SELECT d.id, m.id, i.id
            FROM meal_plan_days d
            LEFT JOIN meal_plan_day_meals m ON m.meal_plan_day_id = d.id
            LEFT JOIN meal_plan_meal_items i ON i.meal_plan_day_meal_id = m.id
            WHERE d.user_id = 1 AND d.plan_date BETWEEN '2026-10-01' AND '2026-10-07'
            ORDER BY d.plan_date, m.sort_order, m.id, i.sort_order, i.id;
            """);

        Assert.NotNull(rangePlan);
        Assert.Contains("uq_meal_plan_days_user_date", rangePlan!, StringComparison.Ordinal);
        Assert.NotNull(copyPlan);
        Assert.NotEmpty(copyPlan!);
        Assert.Equal(3, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND index_name IN (
                  'uq_meal_plan_days_user_date',
                  'idx_meal_plan_day_meals_order',
                  'idx_meal_plan_meal_items_order'
              );
            """));
    }

    private async Task<int> InsertEasyPlanAsync(int userId, string name)
    {
        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO nutrition_plans (
                user_id, name, mode, default_meals_per_day, created_at, updated_at
            ) VALUES (@userId, @name, 'Easy', 3, '{Timestamp}', '{Timestamp}');
            """,
            new { userId, name });
        return (await Factory.Database.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM nutrition_plans WHERE user_id = @userId AND name = @name;",
            new { userId, name }))!.Value;
    }

    private async Task<(int DayId, int MealId)> InsertDayAndMealAsync()
    {
        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_days (user_id, plan_date, created_at, updated_at)
            VALUES (1, '2026-10-05', '{Timestamp}', '{Timestamp}');
            """);
        var dayId = (await Factory.Database.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM meal_plan_days WHERE user_id = 1 AND plan_date = '2026-10-05';"))!.Value;
        await Factory.Database.ExecuteAsync(
            $"""
            INSERT INTO meal_plan_day_meals (
                meal_plan_day_id, name, sort_order, created_at, updated_at
            ) VALUES ({dayId}, 'Breakfast', 1, '{Timestamp}', '{Timestamp}');
            """);
        var mealId = (await Factory.Database.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM meal_plan_day_meals WHERE meal_plan_day_id = @dayId;",
            new { dayId }))!.Value;
        return (dayId, mealId);
    }

    private async Task RemoveB02SchemaAsync() => await Factory.Database.ExecuteAsync(
        """
        DROP TABLE meal_plan_manual_items;
        DROP TABLE meal_plan_meal_items;
        DROP TABLE meal_plan_day_meals;
        DROP TABLE meal_plan_days;
        DROP TABLE meal_plan_user_settings;
        DROP TABLE nutrition_plans;
        ALTER TABLE users
            DROP CHECK chk_users_nutrition_profile_complete,
            DROP COLUMN weight_kg,
            DROP COLUMN height_cm,
            DROP COLUMN birth_year,
            DROP COLUMN calculation_sex;
        """);

    private async Task<IdentitySnapshot> ReadIdentitySnapshotAsync() =>
        (await Factory.Database.QuerySingleOrDefaultAsync<IdentitySnapshot>(
            """
            SELECT
                (SELECT COUNT(*) FROM users) AS UserCount,
                (SELECT SUM(id) FROM users) AS UserIdSum,
                (SELECT COUNT(*) FROM recipes) AS RecipeCount,
                (SELECT SUM(id) FROM recipes) AS RecipeIdSum,
                (SELECT COUNT(*) FROM favorite_recipes) AS FavoriteCount;
            """))!;

    private async Task AssertMySqlErrorAsync(int expectedNumber, Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<MySqlException>(action);
        Assert.Equal(expectedNumber, exception.Number);
    }

    private sealed record IdentitySnapshot(
        long UserCount,
        decimal UserIdSum,
        long RecipeCount,
        decimal RecipeIdSum,
        long FavoriteCount);
}
