// Purpose: Verifies the F-010 Phase 1 authenticated Cooking View API against real MySQL persistence.
// Change reason: Cover compatibility, exact order-independent JSON fields, authored collections, OpenAPI, and SQL planning.
// Created: 2026-08-31T18:01:27-06:00
// Last updated: 2026-08-31T18:36:47-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Infrastructure.Recipes;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises the dedicated Cooking View route without exposing the complete authoring detail contract.</summary>
public sealed class F010Phase1CookingViewTests : IntegrationTestBase
{
    public F010Phase1CookingViewTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task CookingView_RequiresAuthentication()
    {
        using var response = await Client.GetAsync("/api/recipes/1/cooking-view");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookingView_ReturnsCompleteOrderedBaseProjectionWithFullImage()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        using var response = await client.GetAsync("/api/recipes/1/cooking-view");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        var cookingView = JsonSerializer.Deserialize<RecipeCookingViewDto>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(cookingView);
        Assert.Equal(1, cookingView.Id);
        Assert.Equal("Chicken Rice Bowl", cookingView.Name);
        Assert.Equal("https://test.local/recipes/chicken-rice-bowl-seed.avif", cookingView.ImageUrl);
        Assert.Equal("A simple chicken, rice, and broccoli bowl.", cookingView.Description);
        Assert.Equal(25, cookingView.TotalTimeMinutes);
        Assert.Equal(2, cookingView.BaseServings);
        Assert.Equal(674.70m, cookingView.CaloriesTotal);
        Assert.Equal(85.72m, cookingView.ProteinTotal);
        Assert.Equal(54.00m, cookingView.CarbsTotal);
        Assert.Equal(["High Protein", "Budget Friendly"], cookingView.Badges);

        Assert.Equal([1, 2, 3], cookingView.Ingredients.Select(ingredient => ingredient.Id));
        Assert.Equal([1, 2, 3], cookingView.Ingredients.Select(ingredient => ingredient.DisplayOrder));
        Assert.Equal([250.000m, 180.000m, 120.000m], cookingView.Ingredients.Select(ingredient => ingredient.Quantity));
        Assert.All(cookingView.Ingredients, ingredient => Assert.Equal("g", ingredient.Unit));

        Assert.Equal([1, 2, 3], cookingView.Steps.Select(step => step.Order));
        Assert.Equal("Season the chicken", cookingView.Steps[0].Title);
        Assert.Equal("Season and sear the chicken.", cookingView.Steps[0].Instruction);
        Assert.Equal("Cook the sides", cookingView.Steps[1].Title);
        Assert.Equal("Assemble the bowl", cookingView.Steps[2].Title);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(
            new[]
            {
                "id",
                "name",
                "imageUrl",
                "description",
                "totalTimeMinutes",
                "baseServings",
                "caloriesTotal",
                "proteinTotal",
                "carbsTotal",
                "badges",
                "ingredients",
                "steps",
            }.Order(),
            root.EnumerateObject().Select(property => property.Name).Order());
        Assert.All(
            root.GetProperty("ingredients").EnumerateArray(),
            ingredient => Assert.Equal(
                ["id", "name", "quantity", "unit", "displayOrder"],
                ingredient.EnumerateObject().Select(property => property.Name)));
        Assert.All(
            root.GetProperty("steps").EnumerateArray(),
            step => Assert.Equal(
                ["order", "title", "instruction"],
                step.EnumerateObject().Select(property => property.Name)));
    }

    [Fact]
    public async Task CookingView_HidesMissingAndIncompatibleRecipesBehindSameNotFoundResponse()
    {
        var (compatibleClient, compatibleUser) = await CreateAuthenticatedClientAsync(
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

        using var missingResponse = await compatible.GetAsync("/api/recipes/9999/cooking-view");
        using var dietResponse = await dietIncompatible.GetAsync(
            $"/api/recipes/1/cooking-view?currentUserId={compatibleUser.userId}");
        using var restrictionResponse = await restricted.GetAsync("/api/recipes/1/cooking-view");
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
    public async Task CookingView_ValidatesRouteIdAndPublishesDedicatedOpenApiContract()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        using var invalidResponse = await client.GetAsync("/api/recipes/0/cooking-view");
        using var openApiResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        openApiResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await openApiResponse.Content.ReadAsStreamAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes/{id}/cooking-view")
            .GetProperty("get");
        var responses = operation.GetProperty("responses");
        var schemaReference = responses
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        var schemaProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("RecipeCookingViewDto")
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Order();

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.EndsWith("RecipeCookingViewDto", schemaReference, StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "badges",
                "baseServings",
                "caloriesTotal",
                "carbsTotal",
                "description",
                "id",
                "imageUrl",
                "ingredients",
                "name",
                "proteinTotal",
                "steps",
                "totalTimeMinutes",
            },
            schemaProperties);
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.True(responses.TryGetProperty("404", out _));
    }

    [Fact]
    public async Task CookingViewBaseQuery_HasAValidRepresentativeExecutionPlan()
    {
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        var sql = RecipeSql.GetCompatibleCookingView;
        var baseQuery = sql[..sql.IndexOf(';')];

        var planJson = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            $"EXPLAIN FORMAT=JSON {baseQuery}",
            new { CurrentUserId = user.userId, RecipeId = 1 });

        Assert.NotNull(planJson);
        using var plan = JsonDocument.Parse(planJson);
        Assert.True(plan.RootElement.TryGetProperty("query_block", out _));
    }
}
