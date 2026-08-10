using Appetee.Api.Tests.Infrastructure;

namespace Appetee.Api.Tests.Integration;

public sealed class F007Phase2DatabaseTests : IntegrationTestBase
{
    public F007Phase2DatabaseTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CanonicalSchema_RequiresAllCoreCalculationColumns()
    {
        var requiredColumnCount = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND is_nullable = 'NO'
              AND (
                    (table_name = 'ingredient_nutrition'
                     AND column_name IN ('calories_kcal', 'protein_g', 'carbs_g'))
                 OR (table_name = 'recipes'
                     AND column_name = 'estimated_cost_per_serving')
              );
            """);

        Assert.Equal(4, requiredColumnCount);
    }

    [Fact]
    public async Task CanonicalSeedRecipeTotals_MatchIngredientCalculationData()
    {
        var totals = await Factory.Database.QuerySingleOrDefaultAsync<SeedRecipeTotals>(
            """
            SELECT
                calories_total AS CaloriesTotal,
                protein_total AS ProteinTotal,
                carbs_total AS CarbsTotal,
                estimated_cost_per_serving AS EstimatedCostPerServing
            FROM recipes
            WHERE id = 1;
            """);

        Assert.NotNull(totals);
        Assert.Equal(674.70m, totals!.CaloriesTotal);
        Assert.Equal(85.72m, totals.ProteinTotal);
        Assert.Equal(54.00m, totals.CarbsTotal);
        Assert.Equal(4.13m, totals.EstimatedCostPerServing);
    }

    [Fact]
    public async Task ForwardMigration_RecalculatesLegacyRecipeAndMakesCoreColumnsRequired()
    {
        await Factory.Database.ExecuteAsync(
            """
            ALTER TABLE ingredient_nutrition
                MODIFY calories_kcal DECIMAL(10,2) NULL,
                MODIFY protein_g DECIMAL(10,2) NULL,
                MODIFY carbs_g DECIMAL(10,2) NULL;
            ALTER TABLE recipes
                MODIFY estimated_cost_per_serving DECIMAL(10,2) NULL;
            UPDATE recipes
            SET calories_total = 1,
                protein_total = 1,
                carbs_total = 1,
                estimated_cost_per_serving = NULL
            WHERE id = 1;
            """);

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260808_001_f007_phase2_mandatory_calculation_data.sql");

        var requiredColumnCount = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND is_nullable = 'NO'
              AND (
                    (table_name = 'ingredient_nutrition'
                     AND column_name IN ('calories_kcal', 'protein_g', 'carbs_g'))
                 OR (table_name = 'recipes'
                     AND column_name = 'estimated_cost_per_serving')
              );
            """);
        var totals = await Factory.Database.QuerySingleOrDefaultAsync<SeedRecipeTotals>(
            """
            SELECT
                calories_total AS CaloriesTotal,
                protein_total AS ProteinTotal,
                carbs_total AS CarbsTotal,
                estimated_cost_per_serving AS EstimatedCostPerServing
            FROM recipes
            WHERE id = 1;
            """);

        Assert.Equal(4, requiredColumnCount);
        Assert.NotNull(totals);
        Assert.Equal(674.70m, totals!.CaloriesTotal);
        Assert.Equal(85.72m, totals.ProteinTotal);
        Assert.Equal(54.00m, totals.CarbsTotal);
        Assert.Equal(4.13m, totals.EstimatedCostPerServing);
    }

    private sealed record SeedRecipeTotals(
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal,
        decimal EstimatedCostPerServing);
}
