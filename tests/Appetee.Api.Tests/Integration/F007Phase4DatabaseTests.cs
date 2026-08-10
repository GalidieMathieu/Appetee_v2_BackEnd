using Appetee.Api.Tests.Infrastructure;
using MySqlConnector;

namespace Appetee.Api.Tests.Integration;

public sealed class F007Phase4DatabaseTests : IntegrationTestBase
{
    public F007Phase4DatabaseTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ForwardMigration_NormalizesEquivalentUnitsAndRecomputesEveryRecipeTotal()
    {
        await Factory.Database.ExecuteAsync(
            """
            UPDATE recipe_ingredients
            SET unit = ' G '
            WHERE recipe_id = 1 AND ingredient_id = 1;

            UPDATE recipes
            SET calories_total = 1,
                protein_total = 1,
                carbs_total = 1,
                estimated_cost_per_serving = 1
            WHERE id = 1;
            """);

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260809_003_f007_phase4_relationship_cleanup.sql");

        var unit = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            SELECT unit
            FROM recipe_ingredients
            WHERE recipe_id = 1 AND ingredient_id = 1;
            """);
        var totals = await Factory.Database.QuerySingleOrDefaultAsync<RecipeTotals>(
            """
            SELECT
                calories_total AS CaloriesTotal,
                protein_total AS ProteinTotal,
                carbs_total AS CarbsTotal,
                estimated_cost_per_serving AS EstimatedCostPerServing
            FROM recipes
            WHERE id = 1;
            """);

        Assert.Equal("g", unit);
        Assert.NotNull(totals);
        Assert.Equal(674.70m, totals!.CaloriesTotal);
        Assert.Equal(85.72m, totals.ProteinTotal);
        Assert.Equal(54.00m, totals.CarbsTotal);
        Assert.Equal(4.13m, totals.EstimatedCostPerServing);
    }

    [Fact]
    public async Task ForwardMigration_RejectsSemanticUnitMismatchWithoutChangingRecipeTotals()
    {
        await Factory.Database.ExecuteAsync(
            """
            UPDATE recipe_ingredients
            SET unit = 'ml'
            WHERE recipe_id = 1 AND ingredient_id = 1;

            UPDATE recipes
            SET calories_total = 1
            WHERE id = 1;
            """);

        var exception = await Assert.ThrowsAsync<MySqlException>(() =>
            Factory.Database.ExecuteMigrationAsync(
                "migrations/20260809_003_f007_phase4_relationship_cleanup.sql"));
        var calories = await Factory.Database.QuerySingleOrDefaultAsync<decimal>(
            "SELECT calories_total FROM recipes WHERE id = 1;");

        Assert.Contains("semantic unit mismatches", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1m, calories);
    }

    private sealed record RecipeTotals(
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal,
        decimal EstimatedCostPerServing);
}
