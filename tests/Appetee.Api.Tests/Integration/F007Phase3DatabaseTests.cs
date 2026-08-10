using Appetee.Api.Tests.Infrastructure;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F007Phase3DatabaseTests : IntegrationTestBase
{
    public F007Phase3DatabaseTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task CanonicalSeedRecipe_UsesStructuredInstructionObjects()
    {
        var instructionsJson = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            "SELECT instructions FROM recipes WHERE id = 1;");

        Assert.NotNull(instructionsJson);
        using var document = JsonDocument.Parse(instructionsJson!);
        var steps = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(3, steps.Length);
        Assert.Equal("Season the chicken", steps[0].GetProperty("title").GetString());
        Assert.Equal("Season and sear the chicken.", steps[0].GetProperty("instruction").GetString());
        Assert.Equal("Cook the sides", steps[1].GetProperty("title").GetString());
        Assert.Equal("Assemble the bowl", steps[2].GetProperty("title").GetString());
    }

    [Fact]
    public async Task ForwardMigration_ConvertsLegacyStringsAndDiscardsBlankValuesInOrder()
    {
        await Factory.Database.ExecuteAsync(
            """
            UPDATE recipes
            SET instructions = JSON_ARRAY(
                ' Prepare the ingredients. ',
                '   ',
                'Serve the meal.')
            WHERE id = 1;
            """);

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260808_002_f007_phase3_structured_instructions.sql");

        var instructionsJson = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            "SELECT instructions FROM recipes WHERE id = 1;");

        Assert.NotNull(instructionsJson);
        using var document = JsonDocument.Parse(instructionsJson!);
        var steps = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, steps.Length);
        Assert.Equal("Step 1", steps[0].GetProperty("title").GetString());
        Assert.Equal("Prepare the ingredients.", steps[0].GetProperty("instruction").GetString());
        Assert.Equal("Step 3", steps[1].GetProperty("title").GetString());
        Assert.Equal("Serve the meal.", steps[1].GetProperty("instruction").GetString());
    }
}
