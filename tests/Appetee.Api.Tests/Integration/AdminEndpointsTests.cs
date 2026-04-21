using Appetee.Application.Dtos;
using Appetee.Api.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace Appetee.Api.Tests.Integration;

public sealed class AdminEndpointsTests : IntegrationTestBase
{
    public AdminEndpointsTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task IngredientDetails_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/admin/ingredient-details/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetIngredientDetails_ReturnsSeededIngredient()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/admin/ingredient-details/1");
        var ingredient = await response.Content.ReadFromJsonAsync<IngredientAdminDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(ingredient);
        Assert.Equal(1, ingredient!.Id);
        Assert.Equal("Chicken Breast", ingredient.Name);
        Assert.StartsWith("https://test.local/ingredients/", ingredient.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIngredientDetails_ReturnsNotFound_WhenIngredientDoesNotExist()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/admin/ingredient-details/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateIngredientDetails_ReturnsCreatedIngredient()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateIngredientRequest(name: "Test Lentils", price: 2.85m);

        var response = await client.PostAsync("/api/admin/ingredient-details", content);
        var ingredient = await response.Content.ReadFromJsonAsync<IngredientAdminDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(ingredient);
        Assert.True(ingredient!.Id > 4);
        Assert.Equal("Test Lentils", ingredient.Name);
        Assert.StartsWith("https://test.local/ingredients/", ingredient.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateIngredientDetails_ValidatesRequiredImage()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateIngredientRequest(includeImage: false);

        var response = await client.PostAsync("/api/admin/ingredient-details", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Image", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_ReturnsCreatedRecipe()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(name: "Weeknight Chicken Tray Bake");

        var createResponse = await client.PostAsync("/api/admin/recipe-details", content);
        var recipe = await createResponse.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(recipe);
        Assert.True(recipe!.Id > 1);
        Assert.Equal("Weeknight Chicken Tray Bake", recipe.Name);
        Assert.StartsWith("https://test.local/recipes/", recipe.ImageUrl, StringComparison.Ordinal);

        var getResponse = await client.GetAsync($"/api/recipes/{recipe.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(recipe.Id, detail!.Id);
        Assert.Equal(recipe.Name, detail.Name);
    }

    [Fact]
    public async Task CreateRecipeDetails_ValidatesTheBadgeValue()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(badges: new[] { "not-a-valid-badge" });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("invalid badge", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_ValidatesDuplicateIngredientSelection()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            ingredients: new[]
            {
                new RecipeIngredientFormItem(1, 100m, "g"),
                new RecipeIngredientFormItem(1, 120m, "g"),
            });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("selected more than once", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_RequiresIngredientQuantityAndUnit()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = new MultipartFormDataContent();

        content.Add(new StringContent("Broken Recipe"), "Name");
        content.Add(new StringContent("540"), "CaloriesTotal");
        content.Add(new StringContent("31"), "ProteinTotal");
        content.Add(new StringContent("20"), "CarbsTotal");
        content.Add(new StringContent("Mix everything.\nServe."), "Instructions");
        content.Add(new StringContent("20"), "PrepTimeMinutes");
        content.Add(new StringContent("2"), "Servings");
        content.Add(new StringContent("Easy"), "Difficulty");
        content.Add(new StringContent("2"), "DietIds[0]");
        content.Add(new StringContent("1"), "Ingredients[0].IngredientId");

        var image = new ByteArrayContent(new byte[] { 1, 2, 3 });
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "Image", "broken.png");

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("quantity", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_ValidatesReferencedIngredients()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            ingredients: new[]
            {
                new RecipeIngredientFormItem(999, 100m, "g"),
            });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Invalid IngredientIds", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateRecipeDetails_ReturnsUpdatedRecipe()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            name: "Updated Chicken Rice Bowl",
            difficulty: "Hard",
            badges: new[] { "budget-focused" },
            dietIds: new[] { 1 },
            ingredients: new[]
            {
                new RecipeIngredientFormItem(2, 160m, "g"),
                new RecipeIngredientFormItem(3, 150m, "g"),
            },
            includeImage: false);

        var updateResponse = await client.PutAsync("/api/admin/recipe-details/1", content);
        var recipe = await updateResponse.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(recipe);
        Assert.Equal("Updated Chicken Rice Bowl", recipe!.Name);
        Assert.Equal("Hard", recipe.Difficulty);
        Assert.Single(recipe.Diets!);
        Assert.Equal(2, recipe.Ingredients.Count);

        var getResponse = await client.GetAsync("/api/recipes/1");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("Updated Chicken Rice Bowl", detail!.Name);
        Assert.Equal("Hard", detail.Difficulty);
        Assert.Single(detail.Diets!);
    }

    [Fact]
    public async Task UpdateRecipeDetails_ReturnsNotFound_WhenRecipeDoesNotExist()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(includeImage: false);

        var response = await client.PutAsync("/api/admin/recipe-details/999", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
