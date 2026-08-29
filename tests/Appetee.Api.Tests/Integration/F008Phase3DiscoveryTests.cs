// Purpose: Verifies the authenticated, compatibility-scoped F-008 Phase 3 recipe discovery contract.
// Created: 2026-08-24T22:52:54-06:00
// Last updated: 2026-08-25T15:25:01-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class F008Phase3DiscoveryTests : IntegrationTestBase
{
    public F008Phase3DiscoveryTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Discovery_RequiresAuthentication()
    {
        using var response = await Client.GetAsync("/api/recipes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Discovery_ReturnsOnlyTheBoundedCardProjection()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        using var response = await client.GetAsync("/api/recipes");
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        var page = JsonSerializer.Deserialize<RecipeDiscoveryPageDto>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.False(page!.HasMore);
        Assert.Null(page.NextCursor);

        var card = Assert.Single(page.Items);
        Assert.Equal(1, card.Id);
        Assert.Equal("Chicken Rice Bowl", card.Name);
        Assert.StartsWith("https://test.local/", card.CardImageUrl, StringComparison.Ordinal);
        Assert.Equal(new[] { "High Protein", "Budget Friendly" }, card.Badges);
        Assert.Equal(new[] { 1, 2, 3 }, card.FeaturedIngredients.Select(item => item.FeaturedOrder));
        Assert.All(card.FeaturedIngredients, item => Assert.InRange(item.FeaturedOrder, 1, 3));
        Assert.False(card.IsSaved);

        using var document = JsonDocument.Parse(json);
        var cardJson = document.RootElement.GetProperty("items")[0];
        Assert.True(cardJson.TryGetProperty("cardImageUrl", out _));
        Assert.True(cardJson.TryGetProperty("featuredIngredients", out _));
        Assert.False(cardJson.TryGetProperty("imageUrl", out _));
        Assert.False(cardJson.TryGetProperty("previewImageUrl", out _));
        Assert.False(cardJson.TryGetProperty("description", out _));
        Assert.False(cardJson.TryGetProperty("prepTimeMinutes", out _));
        Assert.False(cardJson.TryGetProperty("cookTimeMinutes", out _));
        Assert.False(cardJson.TryGetProperty("servings", out _));
        Assert.False(cardJson.TryGetProperty("difficulty", out _));
        Assert.False(cardJson.TryGetProperty("diets", out _));
        Assert.False(cardJson.TryGetProperty("ingredients", out _));
        Assert.False(cardJson.TryGetProperty("instructions", out _));
        Assert.False(cardJson.TryGetProperty("proteinPerServing", out _));
    }

    [Fact]
    public async Task Discovery_RequiresEveryCurrentUserDiet()
    {
        var (compatibleClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        var (incompatibleClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 1, 2 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var compatible = compatibleClient;
        using var incompatible = incompatibleClient;

        var compatiblePage = await compatible.GetFromJsonAsync<RecipeDiscoveryPageDto>("/api/recipes");
        var incompatiblePage = await incompatible.GetFromJsonAsync<RecipeDiscoveryPageDto>("/api/recipes");

        Assert.Single(compatiblePage!.Items);
        Assert.Empty(incompatiblePage!.Items);
    }

    [Fact]
    public async Task Discovery_ExcludesARecipeContainingAnyRestrictedIngredient()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2 },
            ingredientRestrictionIds: new[] { 1 });
        using var client = authClient;

        var page = await client.GetFromJsonAsync<RecipeDiscoveryPageDto>("/api/recipes");

        Assert.NotNull(page);
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Discovery_SavedProjectionIsScopedToTheCurrentUser()
    {
        var (userAClient, userA) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2 },
            ingredientRestrictionIds: Array.Empty<int>());
        var (userBClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var clientA = userAClient;
        using var clientB = userBClient;

        await Factory.Database.ExecuteAsync(
            "INSERT INTO favorite_recipes (user_id, recipe_id) VALUES (@userId, 1);",
            new { userId = userA.userId });

        var pageA = await clientA.GetFromJsonAsync<RecipeDiscoveryPageDto>("/api/recipes");
        var pageB = await clientB.GetFromJsonAsync<RecipeDiscoveryPageDto>("/api/recipes");

        Assert.True(Assert.Single(pageA!.Items).IsSaved);
        Assert.False(Assert.Single(pageB!.Items).IsSaved);
    }

    [Fact]
    public async Task OpenApi_PublishesDiscoveryPageInsteadOfWholeCatalogArray()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes")
            .GetProperty("get");
        var responseSchema = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        Assert.Equal(
            "#/components/schemas/RecipeDiscoveryPageDto",
            responseSchema.GetProperty("$ref").GetString());

        if (operation.TryGetProperty("parameters", out var parameters))
        {
            Assert.DoesNotContain(
                parameters.EnumerateArray(),
                parameter => string.Equals(
                    parameter.GetProperty("name").GetString(),
                    "userId",
                    StringComparison.OrdinalIgnoreCase));
        }

        var schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        var cardProperties = schemas
            .GetProperty("RecipeCardDto")
            .GetProperty("properties");
        Assert.True(cardProperties.TryGetProperty("cardImageUrl", out _));
        Assert.False(cardProperties.TryGetProperty("imageUrl", out _));
        Assert.False(cardProperties.TryGetProperty("previewImageUrl", out _));

        foreach (var schemaName in new[] { "RecipeSummaryDto", "RecipeDetailDto" })
        {
            var properties = schemas
                .GetProperty(schemaName)
                .GetProperty("properties");
            Assert.True(properties.TryGetProperty("previewImageUrl", out _));
            Assert.False(properties.TryGetProperty("imageUrl", out _));
            Assert.False(properties.TryGetProperty("cardImageUrl", out _));
        }
    }
}
