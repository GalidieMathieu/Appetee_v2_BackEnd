// Purpose: Verifies F-008 Phase 7 favorite ownership, idempotency, compatibility, and saved-only discovery.
// Created: 2026-08-26T17:29:19-06:00
// Last updated: 2026-08-26T17:29:19-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises authenticated favorite mutations and current-user saved-only filtering through MySQL.</summary>
public sealed class F008Phase7FavoriteTests : IntegrationTestBase
{
    public F008Phase7FavoriteTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task FavoriteMutations_RequireAuthentication()
    {
        using var putResponse = await PutFavoriteAsync(Client, 1);
        using var deleteResponse = await Client.DeleteAsync("/api/recipes/1/favorite");

        Assert.Equal(HttpStatusCode.Unauthorized, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task PutFavorite_IsIdempotentAndUpdatesDiscoveryProjection()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        using var firstResponse = await PutFavoriteAsync(client, 1);
        using var secondResponse = await PutFavoriteAsync(client, 1);
        var page = await GetPageAsync(client, "/api/recipes");

        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);
        Assert.Equal(1, await CountFavoriteAsync(user.userId, 1));
        Assert.True(Assert.Single(page.Items).IsSaved);
    }

    [Fact]
    public async Task PutFavorite_ReturnsSameNotFoundForMissingAndIncompatibleRecipe()
    {
        var (compatibleClient, compatibleUser) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        var (incompatibleClient, incompatibleUser) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 1, 2 },
            ingredientRestrictionIds: Array.Empty<int>());
        var (restrictedClient, restrictedUser) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: new[] { 1 });
        using var compatible = compatibleClient;
        using var incompatible = incompatibleClient;
        using var restricted = restrictedClient;

        using var missingResponse = await PutFavoriteAsync(compatible, 9999);
        using var incompatibleResponse = await PutFavoriteAsync(incompatible, 1);
        using var restrictedResponse = await PutFavoriteAsync(restricted, 1);
        var missingProblem = await missingResponse.ReadProblemDetailsAsync();
        var incompatibleProblem = await incompatibleResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, incompatibleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restrictedResponse.StatusCode);
        Assert.Equal(missingProblem!.Detail, incompatibleProblem!.Detail);
        Assert.Equal(0, await CountFavoriteAsync(compatibleUser.userId, 9999));
        Assert.Equal(0, await CountFavoriteAsync(incompatibleUser.userId, 1));
        Assert.Equal(0, await CountFavoriteAsync(restrictedUser.userId, 1));
    }

    [Fact]
    public async Task DeleteFavorite_IsIdempotentAndCanCleanUpAfterCompatibilityChanges()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;
        using var saveResponse = await PutFavoriteAsync(client, 1);
        await Factory.Database.ExecuteAsync(
            "INSERT INTO user_diets (user_id, diet_id) VALUES (@UserId, 1);",
            new { UserId = user.userId });

        using var incompatibleSaveResponse = await PutFavoriteAsync(client, 1);
        using var firstDeleteResponse = await client.DeleteAsync("/api/recipes/1/favorite");
        using var secondDeleteResponse = await client.DeleteAsync("/api/recipes/1/favorite");

        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, incompatibleSaveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, firstDeleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondDeleteResponse.StatusCode);
        Assert.Equal(0, await CountFavoriteAsync(user.userId, 1));
    }

    [Fact]
    public async Task FavoriteMembership_IsOwnedAndReadByCurrentUserOnly()
    {
        var (userAClient, userA) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        var (userBClient, userB) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var clientA = userAClient;
        using var clientB = userBClient;
        using var saveAResponse = await PutFavoriteAsync(clientA, 1);
        using var deleteBResponse = await clientB.DeleteAsync("/api/recipes/1/favorite");

        var savedA = await GetPageAsync(clientA, "/api/recipes?savedOnly=true");
        var savedB = await GetPageAsync(clientB, "/api/recipes?savedOnly=true");

        Assert.Equal(HttpStatusCode.NoContent, saveAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteBResponse.StatusCode);
        Assert.Equal(1, await CountFavoriteAsync(userA.userId, 1));
        Assert.Equal(0, await CountFavoriteAsync(userB.userId, 1));
        Assert.Equal(1, Assert.Single(savedA.Items).Id);
        Assert.Empty(savedB.Items);
    }

    [Fact]
    public async Task SavedOnly_ComposesWithSearchAndIsBoundIntoCursorCriteria()
    {
        await SeedCompatibleRecipeAsync(2, "Favorite Search Recipe");
        await SeedCompatibleRecipeAsync(3, "Unsaved Search Recipe");
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;
        using var saveFirstResponse = await PutFavoriteAsync(client, 1);
        using var saveSecondResponse = await PutFavoriteAsync(client, 2);

        var saved = await GetPageAsync(client, "/api/recipes?savedOnly=true");
        var searched = await GetPageAsync(
            client,
            "/api/recipes?savedOnly=true&search=favorite");
        var firstPage = await GetPageAsync(
            client,
            "/api/recipes?savedOnly=true&limit=1");
        using var changedCriteriaResponse = await client.GetAsync(
            $"/api/recipes?savedOnly=false&limit=1&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}");

        Assert.Equal(HttpStatusCode.NoContent, saveFirstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, saveSecondResponse.StatusCode);
        Assert.Equal(new[] { 1, 2 }, saved.Items.Select(item => item.Id).Order());
        Assert.All(saved.Items, item => Assert.True(item.IsSaved));
        Assert.Equal(2, Assert.Single(searched.Items).Id);
        Assert.True(firstPage.HasMore);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Equal(HttpStatusCode.BadRequest, changedCriteriaResponse.StatusCode);
    }

    [Fact]
    public async Task ConcurrentFavoriteMutations_RemainIdempotent()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var putResponses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => PutFavoriteAsync(client, 1)));
        try
        {
            Assert.All(
                putResponses,
                response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            Assert.Equal(1, await CountFavoriteAsync(user.userId, 1));
        }
        finally
        {
            foreach (var response in putResponses)
                response.Dispose();
        }

        var deleteResponses = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => client.DeleteAsync("/api/recipes/1/favorite")));
        try
        {
            Assert.All(
                deleteResponses,
                response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            Assert.Equal(0, await CountFavoriteAsync(user.userId, 1));
        }
        finally
        {
            foreach (var response in deleteResponses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task FavoriteApi_ValidatesRouteIdAndPublishesSavedOnlyContract()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;
        using var invalidPutResponse = await PutFavoriteAsync(client, 0);
        using var invalidDeleteResponse = await client.DeleteAsync("/api/recipes/0/favorite");
        using var openApiResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        openApiResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await openApiResponse.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");
        var favoritePath = paths.GetProperty("/api/recipes/{id}/favorite");
        var discoveryParameters = paths
            .GetProperty("/api/recipes")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, invalidPutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDeleteResponse.StatusCode);
        Assert.True(favoritePath.TryGetProperty("put", out var putOperation));
        Assert.True(favoritePath.TryGetProperty("delete", out var deleteOperation));
        Assert.True(putOperation.GetProperty("responses").TryGetProperty("204", out _));
        Assert.True(deleteOperation.GetProperty("responses").TryGetProperty("204", out _));
        Assert.Contains("savedOnly", discoveryParameters);
        Assert.DoesNotContain("userId", discoveryParameters);
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

    private async Task SeedCompatibleRecipeAsync(int id, string name)
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
                @Id,
                @Name,
                'Phase 7 favorite fixture.',
                NULL,
                JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook the recipe.')),
                10,
                20,
                30,
                2,
                'Easy',
                4.00,
                800.00,
                60.00,
                100.00,
                400.00,
                30.00
            );
            """,
            new { Id = id, Name = name });
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO diet_recipes (recipe_id, diet_id)
            VALUES (@RecipeId, 2), (@RecipeId, 3);
            """,
            new { RecipeId = id });
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO recipe_ingredients (
                recipe_id,
                ingredient_id,
                quantity,
                unit,
                display_order,
                featured_order,
                note
            ) VALUES (@RecipeId, 2, 100.000, 'g', 1, 1, NULL);
            """,
            new { RecipeId = id });
    }

    private static async Task<HttpResponseMessage> PutFavoriteAsync(
        HttpClient client,
        int recipeId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/recipes/{recipeId}/favorite");
        return await client.SendAsync(request);
    }

    private static async Task<RecipeDiscoveryPageDto> GetPageAsync(
        HttpClient client,
        string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        return JsonSerializer.Deserialize<RecipeDiscoveryPageDto>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Discovery response was empty.");
    }
}
