using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Infrastructure.Recipes;
using Dapper;
using MySqlConnector;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B01RecipeNutritionTests : IntegrationTestBase
{
    public F011B01RecipeNutritionTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task RecipeEndpoints_KeepOrdinaryCardsStableAndReturnAdvancedMealPlanCards()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: [2],
            ingredientRestrictionIds: []);
        using var client = authClient;
        await Factory.Database.ExecuteAsync(
            "INSERT INTO favorite_recipes (user_id, recipe_id) VALUES (@userId, 1);",
            new { userId = user.userId });

        using var ordinaryResponse = await client.GetAsync("/api/recipes?search=Chicken");
        using var favoritesResponse = await client.GetAsync("/api/recipes/favorites");
        using var mealPlanResponse = await client.GetAsync("/api/recipes/meal-plan?search=Chicken");
        await AssertSuccessAsync(ordinaryResponse);
        await AssertSuccessAsync(favoritesResponse);
        await AssertSuccessAsync(mealPlanResponse);

        using var ordinary = JsonDocument.Parse(await ordinaryResponse.Content.ReadAsStringAsync());
        using var favorites = JsonDocument.Parse(await favoritesResponse.Content.ReadAsStringAsync());
        using var mealPlan = JsonDocument.Parse(await mealPlanResponse.Content.ReadAsStringAsync());

        AssertOrdinaryCardShape(ordinary.RootElement.GetProperty("items")[0]);
        AssertOrdinaryCardShape(favorites.RootElement[0]);

        var advanced = mealPlan.RootElement.GetProperty("items")[0];
        Assert.Equal("A simple chicken, rice, and broccoli bowl.", advanced.GetProperty("description").GetString());
        Assert.Equal(42.86m, advanced.GetProperty("proteinPerServing").GetDecimal());
        Assert.Equal(27m, advanced.GetProperty("carbsPerServing").GetDecimal());
        Assert.Equal(5.64m, advanced.GetProperty("fatPerServing").GetDecimal());
        Assert.True(advanced.GetProperty("isSaved").GetBoolean());
    }

    [Fact]
    public async Task MealPlanDiscovery_MatchesOrdinaryFilteringRankingCursorAndSavedOnly()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: [2],
            ingredientRestrictionIds: []);
        using var client = authClient;
        await Factory.Database.ExecuteAsync(
            "INSERT INTO favorite_recipes (user_id, recipe_id) VALUES (@userId, 1);",
            new { userId = user.userId });

        const string criteria = "search=Chicken&ingredientIds=1&ingredientIds=2&requireAllIngredients=true" +
            "&badges=High%20Protein&maxTotalMinutes=30&maxDifficulty=Hard&savedOnly=true&limit=1";
        var ordinary = await client.GetFromJsonAsync<RecipeDiscoveryPageDto>($"/api/recipes?{criteria}");
        var mealPlan = await client.GetFromJsonAsync<RecipeMealPlanPageDto>($"/api/recipes/meal-plan?{criteria}");

        Assert.NotNull(ordinary);
        Assert.NotNull(mealPlan);
        Assert.Equal(ordinary!.Items.Select(item => item.Id), mealPlan!.Items.Select(item => item.Id));
        Assert.Equal(ordinary.HasMore, mealPlan.HasMore);
        Assert.Equal(ordinary.NextCursor, mealPlan.NextCursor);
        Assert.True(Assert.Single(mealPlan.Items).IsSaved);

        var (incompatibleClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [1],
            ingredientRestrictionIds: []);
        using var incompatible = incompatibleClient;
        var incompatiblePage = await incompatible.GetFromJsonAsync<RecipeMealPlanPageDto>(
            "/api/recipes/meal-plan?search=Chicken");
        Assert.Empty(incompatiblePage!.Items);
    }

    [Theory]
    [InlineData("userId")]
    [InlineData("ownerId")]
    public async Task MealPlanDiscovery_RejectsClientSuppliedOwnerIdentity(string parameter)
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2],
            ingredientRestrictionIds: []);
        using var client = authClient;

        using var response = await client.GetAsync($"/api/recipes/meal-plan?{parameter}=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MealPlanDiscovery_DistinguishesUnknownFatFromKnownZero()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2],
            ingredientRestrictionIds: []);
        using var client = authClient;

        await Factory.Database.ExecuteAsync("UPDATE recipes SET fat_total = NULL WHERE id = 1;");
        var unknown = await client.GetFromJsonAsync<RecipeMealPlanPageDto>(
            "/api/recipes/meal-plan?search=Chicken");
        Assert.Null(Assert.Single(unknown!.Items).FatPerServing);

        await Factory.Database.ExecuteAsync("UPDATE recipes SET fat_total = 0 WHERE id = 1;");
        var zero = await client.GetFromJsonAsync<RecipeMealPlanPageDto>(
            "/api/recipes/meal-plan?search=Chicken");
        Assert.Equal(0m, Assert.Single(zero!.Items).FatPerServing);
    }

    [Fact]
    public async Task RecipeAuthoring_PersistsCalculatedFatOnCreateAndUpdate()
    {
        var (authClient, _) = await CreateCatalogueClientAsync();
        using var client = authClient;
        using var createContent = MultipartContentBuilder.CreateRecipeRequest(
            name: "B01 Fat Persistence Recipe");

        using var createResponse = await client.PostAsync("/api/admin/recipe-details", createContent);
        await AssertSuccessAsync(createResponse);
        var created = await createResponse.Content.ReadCatalogueResourceAsync<RecipeSummaryDto>();
        Assert.NotNull(created);

        var createdFat = await Factory.Database.QuerySingleOrDefaultAsync<decimal?>(
            "SELECT fat_total FROM recipes WHERE id = @id;",
            new { id = created!.Id });
        Assert.Equal(9.72m, createdFat);

        using var updateContent = MultipartContentBuilder.CreateRecipeRequest(
            name: "B01 Updated Fat Persistence Recipe",
            ingredients:
            [
                new RecipeIngredientFormItem(2, 160m, "g", 1),
                new RecipeIngredientFormItem(3, 150m, "g", 2),
            ],
            includeImage: false);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match",createResponse.Headers.ETag!.ToString());
        using var updateResponse = await client.PutAsync(
            $"/api/admin/recipe-details/{created.Id}",
            updateContent);
        await AssertSuccessAsync(updateResponse);

        var updatedFat = await Factory.Database.QuerySingleOrDefaultAsync<decimal?>(
            "SELECT fat_total FROM recipes WHERE id = @id;",
            new { id = created.Id });
        Assert.Equal(2.20m, updatedFat);
    }

    [Fact]
    public async Task MealPlanDiscovery_QueryHasAValidMySqlExecutionPlan()
    {
        var query = new RecipeDiscoveryQuery(
            CurrentUserId: 1,
            PageSize: 20,
            NormalizedSearch: "chicken",
            EffectiveSearchTerms: ["chicken"],
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: null,
            AfterRank: null,
            AfterRecipeId: null);
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(
            query,
            includeMealPlanProjection: true);

        await using var connection = new MySqlConnection(Factory.Database.ConnectionString);
        await connection.OpenAsync();
        var plan = await connection.QueryAsync($"EXPLAIN {sql}", parameters);

        Assert.NotEmpty(plan);
    }

    [Fact]
    public async Task Migration_BackfillsKnownFatAndPreservesRecipeAndFavoriteIdentity()
    {
        await Factory.Database.ExecuteAsync(
            "INSERT INTO favorite_recipes (user_id, recipe_id) VALUES (1, 1);");
        var before = await Factory.Database.QuerySingleOrDefaultAsync<IdentitySnapshot>(
            "SELECT (SELECT id FROM recipes WHERE id = 1) AS RecipeId, " +
            "(SELECT COUNT(*) FROM favorite_recipes WHERE user_id = 1 AND recipe_id = 1) AS FavoriteCount;");
        await RemoveFatColumnAsync();

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20261004_008_f011_recipe_fat.sql");
        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20261004_008_f011_recipe_fat.sql");

        var after = await Factory.Database.QuerySingleOrDefaultAsync<MigrationSnapshot>(
            "SELECT id AS RecipeId, fat_total AS FatTotal, " +
            "(SELECT COUNT(*) FROM favorite_recipes WHERE user_id = 1 AND recipe_id = 1) AS FavoriteCount " +
            "FROM recipes WHERE id = 1;");
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(before!.RecipeId, after!.RecipeId);
        Assert.Equal(before.FavoriteCount, after.FavoriteCount);
        Assert.Equal(11.28m, after.FatTotal);
    }

    [Fact]
    public async Task Migration_LeavesIncompleteFatNullAndAddsNonnegativeConstraint()
    {
        await Factory.Database.ExecuteAsync(
            "UPDATE ingredient_nutrition SET fat_g = NULL WHERE ingredient_id = 2;");
        await RemoveFatColumnAsync();

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20261004_008_f011_recipe_fat.sql");

        var fat = await Factory.Database.QuerySingleOrDefaultAsync<decimal?>(
            "SELECT fat_total FROM recipes WHERE id = 1;");
        Assert.Null(fat);
        var exception = await Assert.ThrowsAsync<MySqlException>(() =>
            Factory.Database.ExecuteAsync("UPDATE recipes SET fat_total = -0.01 WHERE id = 1;"));
        Assert.Equal(3819, exception.Number);
    }

    [Fact]
    public async Task Migration_PreservesKnownZeroFat()
    {
        await Factory.Database.ExecuteAsync(
            """
            UPDATE ingredient_nutrition n
            INNER JOIN recipe_ingredients ri ON ri.ingredient_id = n.ingredient_id
            SET n.fat_g = 0
            WHERE ri.recipe_id = 1;
            """);
        await RemoveFatColumnAsync();

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20261004_008_f011_recipe_fat.sql");

        var fat = await Factory.Database.QuerySingleOrDefaultAsync<decimal?>(
            "SELECT fat_total FROM recipes WHERE id = 1;");
        Assert.Equal(0m, fat);
    }

    [Fact]
    public async Task OpenApi_PublishesDedicatedMealPlanPageWithoutChangingRecipeCardDto()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var ordinary = schemas.GetProperty("RecipeCardDto").GetProperty("properties");
        Assert.False(ordinary.TryGetProperty("description", out _));
        Assert.False(ordinary.TryGetProperty("proteinPerServing", out _));
        Assert.False(ordinary.TryGetProperty("carbsPerServing", out _));
        Assert.False(ordinary.TryGetProperty("fatPerServing", out _));

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes/meal-plan")
            .GetProperty("get");
        var responseReference = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith("RecipeMealPlanPageDto", responseReference, StringComparison.Ordinal);
        Assert.DoesNotContain(
            operation.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() is "userId" or "ownerId");
    }

    private async Task RemoveFatColumnAsync()
    {
        await Factory.Database.ExecuteAsync(
            """
            ALTER TABLE recipes
                DROP CHECK chk_recipes_fat_total,
                DROP COLUMN fat_total;
            """);
    }

    private static void AssertOrdinaryCardShape(JsonElement card)
    {
        Assert.False(card.TryGetProperty("description", out _));
        Assert.False(card.TryGetProperty("proteinPerServing", out _));
        Assert.False(card.TryGetProperty("carbsPerServing", out _));
        Assert.False(card.TryGetProperty("fatPerServing", out _));
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private sealed record IdentitySnapshot(int RecipeId, long FavoriteCount);

    private sealed record MigrationSnapshot(int RecipeId, decimal? FatTotal, long FavoriteCount);
}
