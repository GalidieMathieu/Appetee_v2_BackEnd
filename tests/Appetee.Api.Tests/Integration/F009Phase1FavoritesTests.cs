// Purpose: Verifies the F-009 Phase 1 Favorites collection API against real MySQL persistence.
// Change reason: Add end-to-end coverage for ownership, compatibility, ordering, limits, hydration, and OpenAPI.
// Created: 2026-08-29T14:05:58-06:00
// Last updated: 2026-08-29T14:05:58-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises the authenticated current-user Favorites card-list contract.</summary>
public sealed class F009Phase1FavoritesTests : IntegrationTestBase
{
    public F009Phase1FavoritesTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Favorites_RequiresAuthentication()
    {
        using var response = await Client.GetAsync("/api/recipes/favorites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Favorites_ReturnsEmptyJsonArrayForCurrentUserWithoutFavorites()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        using var response = await client.GetAsync("/api/recipes/favorites");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task Favorites_IsOwnedCompatibleOrderedLimitedAndCanonicallyHydrated()
    {
        var (userAClient, userA) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        var (userBClient, userB) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var clientA = userAClient;
        using var clientB = userBClient;

        foreach (var recipeId in Enumerable.Range(2, 5))
            await SeedRecipeAsync(recipeId, compatible: true);
        await SeedRecipeAsync(7, compatible: false);
        await SeedCardRelationshipsAsync(4);

        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO favorite_recipes (user_id, recipe_id, created_at) VALUES
                (@UserA, 2, '2026-01-02 00:00:00'),
                (@UserA, 3, '2026-01-02 00:00:00'),
                (@UserA, 4, '2026-01-03 00:00:00'),
                (@UserA, 5, '2026-01-01 00:00:00'),
                (@UserA, 6, '2025-12-31 00:00:00'),
                (@UserA, 7, '2026-01-04 00:00:00'),
                (@UserB, 1, '2026-01-05 00:00:00');
            """,
            new { UserA = userA.userId, UserB = userB.userId });

        var full = await GetFavoritesAsync(clientA, "/api/recipes/favorites");
        var limited = await GetFavoritesAsync(clientA, "/api/recipes/favorites?limit=4");
        var userBSelectionAttempt = await GetFavoritesAsync(
            clientB,
            $"/api/recipes/favorites?userId={userA.userId}");

        Assert.Equal([4, 3, 2, 5, 6], full.Select(card => card.Id));
        Assert.Equal(full.Take(4).Select(card => card.Id), limited.Select(card => card.Id));
        Assert.All(full, card => Assert.True(card.IsSaved));
        Assert.Equal([1], userBSelectionAttempt.Select(card => card.Id));

        var hydrated = full[0];
        Assert.Equal(["High Protein", "High Fiber"], hydrated.Badges);
        Assert.Equal([3, 2], hydrated.FeaturedIngredients.Select(ingredient => ingredient.Id));
        Assert.Equal([1, 2], hydrated.FeaturedIngredients.Select(ingredient => (int)ingredient.FeaturedOrder));
        Assert.Equal(
            1,
            await CountFavoriteAsync(userA.userId, 7));
    }

    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(50, HttpStatusCode.OK)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(51, HttpStatusCode.BadRequest)]
    public async Task Favorites_ValidatesOptionalLimit(int limit, HttpStatusCode expected)
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        using var response = await client.GetAsync($"/api/recipes/favorites?limit={limit}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Favorites_PublishesExistingRecipeCardArrayContract()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes/favorites")
            .GetProperty("get");
        var parameters = operation
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()!)
            .ToArray();
        var successSchema = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        Assert.Equal(["limit"], parameters);
        Assert.Equal("array", successSchema.GetProperty("type").GetString());
        Assert.EndsWith(
            "RecipeCardDto",
            successSchema.GetProperty("items").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);
        Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
    }

    [Fact]
    public async Task FavoriteCandidateQuery_HasAValidRepresentativeExecutionPlan()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        using var saveResponse = await client.PutAsync("/api/recipes/1/favorite", null);
        saveResponse.EnsureSuccessStatusCode();

        var planJson = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            $"EXPLAIN FORMAT=JSON {RecipeSql.GetFavoriteCandidatesWithLimit}",
            new { CurrentUserId = user.userId, Limit = 4 });

        Assert.NotNull(planJson);
        using var plan = JsonDocument.Parse(planJson);
        Assert.True(plan.RootElement.TryGetProperty("query_block", out _));
    }

    /// <summary>Reads one successful Favorites collection as the canonical Recipe Card array.</summary>
    private static async Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
        HttpClient client,
        string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        return JsonSerializer.Deserialize<IReadOnlyList<RecipeCardDto>>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Favorites response was null.");
    }

    /// <summary>Creates a lightweight recipe with either the user's full diet set or an intentionally incomplete set.</summary>
    private async Task SeedRecipeAsync(int recipeId, bool compatible)
    {
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO recipes (
                id,
                name,
                description,
                image_blob_name,
                instructions,
                prep_time_minutes,
                cook_time_minutes,
                total_time_minutes,
                servings,
                difficulty,
                estimated_cost_per_serving,
                calories_total,
                protein_total,
                carbs_total,
                calories_per_serving,
                protein_per_serving
            ) VALUES (
                @RecipeId,
                CONCAT('F-009 Recipe ', @RecipeId),
                'F-009 Favorites fixture.',
                NULL,
                JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                5,
                10,
                15,
                2,
                'Easy',
                3.50,
                600.00,
                40.00,
                80.00,
                300.00,
                20.00
            );

            INSERT INTO diet_recipes (recipe_id, diet_id)
            VALUES (@RecipeId, 2);
            """,
            new { RecipeId = recipeId });

        if (compatible)
        {
            await Factory.Database.ExecuteAsync(
                "INSERT INTO diet_recipes (recipe_id, diet_id) VALUES (@RecipeId, 3);",
                new { RecipeId = recipeId });
        }
    }

    /// <summary>Adds deliberately noncanonical child insertion order to verify shared hydration ordering.</summary>
    private async Task SeedCardRelationshipsAsync(int recipeId)
    {
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO recipe_badges (recipe_id, badge) VALUES
                (@RecipeId, 'High Fiber'),
                (@RecipeId, 'High Protein');

            INSERT INTO recipe_ingredients (
                recipe_id,
                ingredient_id,
                quantity,
                unit,
                display_order,
                featured_order,
                note
            ) VALUES
                (@RecipeId, 2, 100.000, 'g', 1, 2, NULL),
                (@RecipeId, 3, 100.000, 'g', 2, 1, NULL);
            """,
            new { RecipeId = recipeId });
    }

    private async Task<int> CountFavoriteAsync(int userId, int recipeId) =>
        await Factory.Database.QuerySingleOrDefaultAsync<int>(
            """
            SELECT COUNT(*)
            FROM favorite_recipes
            WHERE user_id = @UserId
              AND recipe_id = @RecipeId;
            """,
            new { UserId = userId, RecipeId = recipeId });
}
