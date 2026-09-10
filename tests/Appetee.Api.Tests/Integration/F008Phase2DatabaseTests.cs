/*
 * Purpose: Verifies the F-008 Phase 2 recipe-readiness schema, seed data, and ordering constraints.
 * Created: 2026-08-24T22:14:25-06:00
 * Last updated: 2026-08-24T22:24:00-06:00
 */

using Appetee.Api.Tests.Infrastructure;
using MySqlConnector;

namespace Appetee.Api.Tests.Integration;

public sealed class F008Phase2DatabaseTests : IntegrationTestBase
{
    public F008Phase2DatabaseTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CanonicalSeed_HasCompleteRecipeReadinessAndIngredientOrder()
    {
        var recipe = await Factory.Database.QuerySingleOrDefaultAsync<RecipeReadinessRow>(
            """
            SELECT
                description AS Description,
                prep_time_minutes AS PrepTimeMinutes,
                cook_time_minutes AS CookTimeMinutes,
                total_time_minutes AS TotalTimeMinutes,
                calories_per_serving AS CaloriesPerServing,
                protein_per_serving AS ProteinPerServing
            FROM recipes
            WHERE id = 1;
            """);
        var ingredientOrders = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            SELECT GROUP_CONCAT(
                CONCAT(display_order, ':', featured_order)
                ORDER BY display_order
                SEPARATOR ','
            )
            FROM recipe_ingredients
            WHERE recipe_id = 1;
            """);

        Assert.NotNull(recipe);
        Assert.False(string.IsNullOrWhiteSpace(recipe!.Description));
        Assert.Equal(10, recipe.PrepTimeMinutes);
        Assert.Equal(15, recipe.CookTimeMinutes);
        Assert.Equal(25, recipe.TotalTimeMinutes);
        Assert.Equal(337.35m, recipe.CaloriesPerServing);
        Assert.Equal(42.86m, recipe.ProteinPerServing);
        Assert.Equal("1:1,2:2,3:3", ingredientOrders);
    }

    [Fact]
    public async Task Schema_RejectsDuplicateDisplayAndFeaturedOrders()
    {
        var duplicateDisplay = await Assert.ThrowsAsync<MySqlException>(() =>
            Factory.Database.ExecuteAsync(
                """
                UPDATE recipe_ingredients
                SET display_order = 2
                WHERE recipe_id = 1 AND ingredient_id = 1;
                """));
        var duplicateFeatured = await Assert.ThrowsAsync<MySqlException>(() =>
            Factory.Database.ExecuteAsync(
                """
                UPDATE recipe_ingredients
                SET featured_order = 2
                WHERE recipe_id = 1 AND ingredient_id = 1;
                """));

        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, duplicateDisplay.ErrorCode);
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, duplicateFeatured.ErrorCode);
    }

    [Fact]
    public async Task Schema_RejectsOutOfRangeFeaturedOrder()
    {
        var exception = await Assert.ThrowsAsync<MySqlException>(() =>
            Factory.Database.ExecuteAsync(
                """
                UPDATE recipe_ingredients
                SET featured_order = 4
                WHERE recipe_id = 1 AND ingredient_id = 1;
                """));

        Assert.Equal(3819, exception.Number);
    }

    [Fact]
    public async Task ForwardMigration_BackfillsLegacyRecipeDataAndCanonicalBadges()
    {
        await Factory.Database.ExecuteAsync(
            """
            ALTER TABLE recipe_ingredients
                DROP CHECK chk_recipe_ingredients_display_order,
                DROP CHECK chk_recipe_ingredients_featured_order,
                DROP INDEX uq_recipe_ingredients_display_order,
                DROP INDEX uq_recipe_ingredients_featured_order,
                DROP COLUMN featured_order,
                DROP COLUMN display_order;

            ALTER TABLE recipes
                DROP CHECK chk_recipes_description,
                DROP CHECK chk_recipes_cook_time,
                DROP CHECK chk_recipes_total_time,
                DROP CHECK chk_recipes_calories_per_serving,
                DROP CHECK chk_recipes_protein_per_serving,
                DROP CHECK chk_recipes_prep_time,
                DROP COLUMN description,
                DROP COLUMN cook_time_minutes,
                DROP COLUMN total_time_minutes,
                DROP COLUMN calories_per_serving,
                DROP COLUMN protein_per_serving,
                ADD CONSTRAINT chk_recipes_prep_time CHECK (prep_time_minutes > 0);

            ALTER TABLE recipe_badges
                DROP CHECK chk_recipe_badges_badge;

            UPDATE recipe_badges
            SET badge = CASE badge
                WHEN 'High Protein' THEN 'high-protein'
                WHEN 'Budget Friendly' THEN 'budget-focused'
                ELSE badge
            END;

            ALTER TABLE recipe_badges
                ADD CONSTRAINT chk_recipe_badges_badge CHECK (
                    badge IN ('freezer-friendly', 'budget-focused', 'high-protein')
                );
            """);

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260824_006_f008_phase2_recipe_data_readiness.sql");

        var readinessCount = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(*)
            FROM recipes
            WHERE CHAR_LENGTH(TRIM(description)) > 0
              AND total_time_minutes >= prep_time_minutes
              AND calories_per_serving >= 0
              AND protein_per_serving >= 0;
            """);
        var ingredientOrders = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            SELECT GROUP_CONCAT(
                CONCAT(display_order, ':', featured_order)
                ORDER BY display_order
                SEPARATOR ','
            )
            FROM recipe_ingredients
            WHERE recipe_id = 1;
            """);
        var badges = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            SELECT GROUP_CONCAT(badge ORDER BY badge SEPARATOR ',')
            FROM recipe_badges
            WHERE recipe_id = 1;
            """);

        Assert.Equal(1, readinessCount);
        Assert.Equal("1:1,2:2,3:3", ingredientOrders);
        Assert.Equal("Budget Friendly,High Protein", badges);
    }

    private sealed record RecipeReadinessRow(
        string Description,
        int PrepTimeMinutes,
        int CookTimeMinutes,
        int TotalTimeMinutes,
        decimal CaloriesPerServing,
        decimal ProteinPerServing);
}
