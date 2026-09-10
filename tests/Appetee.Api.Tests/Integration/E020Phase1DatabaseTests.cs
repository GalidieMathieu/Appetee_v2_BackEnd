using Appetee.Api.Tests.Infrastructure;

namespace Appetee.Api.Tests.Integration;

public sealed class E020Phase1DatabaseTests : IntegrationTestBase
{
    public E020Phase1DatabaseTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ForwardMigration_AddsNullableRecipeCardImageColumn()
    {
        await Factory.Database.ExecuteAsync(
            "ALTER TABLE recipes DROP COLUMN card_image_blob_name;");

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260820_004_e020_phase1_recipe_card_image.sql");

        var column = await Factory.Database.QuerySingleOrDefaultAsync<ColumnDefinition>(
            """
            SELECT
                DATA_TYPE AS DataType,
                CHARACTER_MAXIMUM_LENGTH AS CharacterMaximumLength,
                IS_NULLABLE AS IsNullable
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'recipes'
              AND column_name = 'card_image_blob_name';
            """);

        Assert.NotNull(column);
        Assert.Equal("varchar", column!.DataType);
        Assert.Equal(500, column.CharacterMaximumLength);
        Assert.Equal("YES", column.IsNullable);
    }

    private sealed record ColumnDefinition(
        string DataType,
        long CharacterMaximumLength,
        string IsNullable);
}
