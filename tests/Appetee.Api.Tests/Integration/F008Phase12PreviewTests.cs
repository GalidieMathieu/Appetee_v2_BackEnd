// Purpose: Verifies the F-008 Phase 12 authenticated Quick Preview API against real MySQL persistence.
// Change reason: Add end-to-end coverage for compatibility, projection boundaries, ordering, ownership, and OpenAPI.
// Created: 2026-08-28T11:50:10-06:00
// Last updated: 2026-08-28T11:50:10-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises the dedicated Recipe Preview route without using the complete authoring detail contract.</summary>
public sealed class F008Phase12PreviewTests : IntegrationTestBase
{
    public F008Phase12PreviewTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Preview_RequiresAuthentication()
    {
        using var response = await Client.GetAsync("/api/recipes/1/preview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Preview_ReturnsLightweightOrderedProjectionWithFullImage()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        using var response = await client.GetAsync("/api/recipes/1/preview");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var preview = JsonSerializer.Deserialize<RecipePreviewDto>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(preview);
        Assert.Equal(1, preview.Id);
        Assert.Equal("Chicken Rice Bowl", preview.Name);
        Assert.Equal("A simple chicken, rice, and broccoli bowl.", preview.Description);
        Assert.Equal("https://test.local/recipes/chicken-rice-bowl-seed.avif", preview.PreviewImageUrl);
        Assert.Equal(25, preview.TotalTimeMinutes);
        Assert.Equal(337.35m, preview.CaloriesPerServing);
        Assert.Equal(42.86m, preview.ProteinPerServing);
        Assert.Equal(4.13m, preview.EstimatedCostPerServing);
        Assert.Equal(["High Protein", "Budget Friendly"], preview.Badges);
        Assert.Equal([1, 2, 3], preview.Ingredients.Select(ingredient => ingredient.Id));
        Assert.False(preview.IsSaved);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        foreach (var excludedProperty in new[]
        {
            "instructions",
            "diets",
            "difficulty",
            "servings",
            "prepTimeMinutes",
            "cookTimeMinutes",
            "caloriesTotal",
            "proteinTotal",
            "carbsTotal",
        })
        {
            Assert.False(root.TryGetProperty(excludedProperty, out _));
        }

        foreach (var ingredient in root.GetProperty("ingredients").EnumerateArray())
        {
            Assert.Equal(["id", "name"], ingredient.EnumerateObject().Select(property => property.Name));
        }
    }

    [Fact]
    public async Task Preview_ReturnsNonNullEmptyCollections()
    {
        await SeedRecipeWithoutChildrenAsync();
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        var preview = await GetPreviewAsync(client, 2);

        Assert.NotNull(preview.Badges);
        Assert.Empty(preview.Badges);
        Assert.NotNull(preview.Ingredients);
        Assert.Empty(preview.Ingredients);
    }

    [Fact]
    public async Task Preview_SavedProjectionIsCurrentUserSpecific()
    {
        var (userAClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        var (userBClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var clientA = userAClient;
        using var clientB = userBClient;
        using var saveResponse = await clientA.PutAsync("/api/recipes/1/favorite", null);
        saveResponse.EnsureSuccessStatusCode();

        var previewA = await GetPreviewAsync(clientA, 1);
        var previewB = await GetPreviewAsync(clientB, 1);

        Assert.True(previewA.IsSaved);
        Assert.False(previewB.IsSaved);
    }

    [Fact]
    public async Task Preview_HidesMissingAndIncompatibleRecipesBehindSameNotFoundResponse()
    {
        var (compatibleClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        var (dietIncompatibleClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [1, 2],
            ingredientRestrictionIds: []);
        var (restrictedClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: [1]);
        using var compatible = compatibleClient;
        using var dietIncompatible = dietIncompatibleClient;
        using var restricted = restrictedClient;

        using var missingResponse = await compatible.GetAsync("/api/recipes/9999/preview");
        using var dietResponse = await dietIncompatible.GetAsync("/api/recipes/1/preview");
        using var restrictionResponse = await restricted.GetAsync("/api/recipes/1/preview");
        var missingProblem = await missingResponse.ReadProblemDetailsAsync();
        var dietProblem = await dietResponse.ReadProblemDetailsAsync();
        var restrictionProblem = await restrictionResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, dietResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restrictionResponse.StatusCode);
        Assert.Equal(missingProblem!.Detail, dietProblem!.Detail);
        Assert.Equal(missingProblem.Detail, restrictionProblem!.Detail);
    }

    [Fact]
    public async Task Preview_ValidatesRouteIdAndPublishesDedicatedOpenApiContract()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        using var invalidResponse = await client.GetAsync("/api/recipes/0/preview");
        using var openApiResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        openApiResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await openApiResponse.Content.ReadAsStreamAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes/{id}/preview")
            .GetProperty("get");
        var responses = operation.GetProperty("responses");
        var schemaReference = responses
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.EndsWith("RecipePreviewDto", schemaReference, StringComparison.Ordinal);
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("404", out _));
    }

    /// <summary>Reads and deserializes one successful authenticated Recipe Preview.</summary>
    private static async Task<RecipePreviewDto> GetPreviewAsync(HttpClient client, int recipeId)
    {
        using var response = await client.GetAsync($"/api/recipes/{recipeId}/preview");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        return JsonSerializer.Deserialize<RecipePreviewDto>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Preview response was empty.");
    }

    /// <summary>Adds a compatible recipe with no badges or ingredients to verify non-null empty arrays.</summary>
    private async Task SeedRecipeWithoutChildrenAsync()
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
                2,
                'Empty Preview Recipe',
                'A fixture with empty Preview collections.',
                NULL,
                JSON_ARRAY(JSON_OBJECT('title', 'Wait', 'instruction', 'Wait.')),
                0,
                5,
                5,
                1,
                'Easy',
                0.00,
                0.00,
                0.00,
                0.00,
                0.00,
                0.00
            );

            INSERT INTO diet_recipes (recipe_id, diet_id) VALUES
                (2, 2),
                (2, 3);
            """);
    }
}
