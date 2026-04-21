using Appetee.Application.Dtos;
using Appetee.Api.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class RecipesEndpointsTests : IntegrationTestBase
{
    public RecipesEndpointsTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetRecipe_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/recipes/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRecipe_ReturnsSeededRecipeWithDetails()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/recipes/1");
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(recipe);
        Assert.Equal(1, recipe!.Id);
        Assert.Equal("Chicken Rice Bowl", recipe.Name);
        Assert.Equal("Medium", recipe.Difficulty);
        Assert.Equal(3, recipe.Instructions.Count);
        Assert.Equal(3, recipe.Ingredients.Count);
        Assert.Contains(recipe.Badges!, badge => badge == "high-protein");
        Assert.Contains(recipe.Diets!, diet => diet.id == 2);
        Assert.Equal("Chicken Breast", recipe.Ingredients[0].Ingredient.Name);
        Assert.StartsWith("https://test.local/recipes/", recipe.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRecipe_ReturnsBadRequest_WhenIdIsZero()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/recipes/0");
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("greater than zero", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRecipe_ReturnsNotFound_WhenRecipeDoesNotExist()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/recipes/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
