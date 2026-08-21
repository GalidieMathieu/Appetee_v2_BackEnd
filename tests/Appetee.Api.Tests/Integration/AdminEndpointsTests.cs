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

    [Theory]
    [InlineData("price")]
    [InlineData("calories")]
    [InlineData("protein")]
    [InlineData("carbs")]
    public async Task CreateIngredientDetails_RequiresEveryCoreCalculationValue(string missingField)
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateIngredientRequest(
            price: missingField == "price" ? null : 3.25m,
            caloriesKcal: missingField == "calories" ? null : 210m,
            proteinG: missingField == "protein" ? null : 12m,
            carbsG: missingField == "carbs" ? null : 5m);

        var response = await client.PostAsync("/api/admin/ingredient-details", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(4, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM ingredients;"));
    }

    [Theory]
    [InlineData("price")]
    [InlineData("calories")]
    [InlineData("protein")]
    [InlineData("carbs")]
    public async Task CreateIngredientDetails_RejectsNegativeCoreCalculationValue(string negativeField)
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateIngredientRequest(
            price: negativeField == "price" ? -1m : 3.25m,
            caloriesKcal: negativeField == "calories" ? -1m : 210m,
            proteinG: negativeField == "protein" ? -1m : 12m,
            carbsG: negativeField == "carbs" ? -1m : 5m);

        var response = await client.PostAsync("/api/admin/ingredient-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("negative", problem!.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM ingredients;"));
    }

    [Fact]
    public async Task CreateIngredientDetails_AcceptsZeroCoreCalculationValues()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateIngredientRequest(
            name: "Zero Core Ingredient",
            price: 0m,
            caloriesKcal: 0m,
            proteinG: 0m,
            carbsG: 0m);

        var response = await client.PostAsync("/api/admin/ingredient-details", content);
        var ingredient = await response.Content.ReadFromJsonAsync<IngredientAdminDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(ingredient);
        Assert.Equal(0m, ingredient!.Price);
        Assert.Equal(0m, ingredient.CaloriesKcal);
        Assert.Equal(0m, ingredient.ProteinG);
        Assert.Equal(0m, ingredient.CarbsG);
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
        Assert.Equal(584.40m, recipe.CaloriesTotal);
        Assert.Equal(73.06m, recipe.ProteinTotal);
        Assert.Equal(46.08m, recipe.CarbsTotal);
        Assert.Equal(2.15m, recipe.EstimatedCostPerServing);

        var getResponse = await client.GetAsync($"/api/recipes/{recipe.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(recipe.Id, detail!.Id);
        Assert.Equal(recipe.Name, detail.Name);
        Assert.Equal(recipe.CaloriesTotal, detail.CaloriesTotal);
        Assert.Equal(recipe.ProteinTotal, detail.ProteinTotal);
        Assert.Equal(recipe.CarbsTotal, detail.CarbsTotal);
        Assert.Equal(recipe.EstimatedCostPerServing, detail.EstimatedCostPerServing);
    }

    [Fact]
    public async Task CreateRecipeDetails_IgnoresForgedSubmittedTotals()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            submittedCaloriesTotal: -999m,
            submittedProteinTotal: 999999m,
            submittedCarbsTotal: 0.01m,
            submittedEstimatedCostPerServing: 50000m);

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(recipe);
        Assert.Equal(584.40m, recipe!.CaloriesTotal);
        Assert.Equal(73.06m, recipe.ProteinTotal);
        Assert.Equal(46.08m, recipe.CarbsTotal);
        Assert.Equal(2.15m, recipe.EstimatedCostPerServing);

        var persisted = await Factory.Database.QuerySingleOrDefaultAsync<RecipeTotalsRow>(
            """
            SELECT
                calories_total AS CaloriesTotal,
                protein_total AS ProteinTotal,
                carbs_total AS CarbsTotal,
                estimated_cost_per_serving AS EstimatedCostPerServing
            FROM recipes
            WHERE id = @Id;
            """,
            new { recipe.Id });

        Assert.NotNull(persisted);
        Assert.Equal(recipe.CaloriesTotal, persisted!.CaloriesTotal);
        Assert.Equal(recipe.ProteinTotal, persisted.ProteinTotal);
        Assert.Equal(recipe.CarbsTotal, persisted.CarbsTotal);
        Assert.Equal(recipe.EstimatedCostPerServing, persisted.EstimatedCostPerServing);
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
    public async Task CreateRecipeDetails_RequiresAtLeastOneInstruction()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            name: "Instructionless Recipe",
            instructions: Array.Empty<RecipeInstructionFormItem>());

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("complete instruction step", problem!.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM recipes WHERE name = 'Instructionless Recipe';"));
    }

    [Fact]
    public async Task CreateRecipeDetails_DiscardsFullyBlankInstructionAndPreservesRemainingOrder()
    {
        var expectedInstructions = new[]
        {
            new RecipeInstructionStepDto("Prepare", "Prepare ingredients."),
            new RecipeInstructionStepDto("Serve", "Serve."),
        };
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            name: "Blank Step Recipe",
            instructions: new[]
            {
                new RecipeInstructionFormItem("Prepare", "Prepare ingredients."),
                new RecipeInstructionFormItem("   ", ""),
                new RecipeInstructionFormItem("Serve", "Serve."),
            });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var created = await response.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(created);

        var detail = await client.GetFromJsonAsync<RecipeDetailDto>($"/api/recipes/{created!.Id}");
        Assert.NotNull(detail);
        Assert.Equal(expectedInstructions, detail!.Instructions);
    }

    [Theory]
    [InlineData("Prepare", "")]
    [InlineData("", "Prepare ingredients.")]
    public async Task CreateRecipeDetails_RejectsPartiallyCompletedInstruction(string title, string instruction)
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            name: "Partial Step Recipe",
            instructions: new[] { new RecipeInstructionFormItem(title, instruction) });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("both title and instruction", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_TrimsAndPreservesInstructionOrder()
    {
        var expectedInstructions = new[]
        {
            new RecipeInstructionStepDto("Prepare ingredients", "Prepare ingredients."),
            new RecipeInstructionStepDto("Cook gently", "Cook gently."),
            new RecipeInstructionStepDto("Serve immediately", "Serve immediately."),
        };
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            instructions: new[]
            {
                new RecipeInstructionFormItem("  Prepare ingredients  ", " Prepare ingredients. "),
                new RecipeInstructionFormItem("Cook gently", "Cook gently."),
                new RecipeInstructionFormItem(" Serve immediately ", "  Serve immediately. "),
            });

        var createResponse = await client.PostAsync("/api/admin/recipe-details", content);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/api/recipes/{created!.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(expectedInstructions, detail!.Instructions);
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
        content.Add(new StringContent("Mix"), "Instructions[0].Title");
        content.Add(new StringContent("Mix everything."), "Instructions[0].Instruction");
        content.Add(new StringContent("Serve"), "Instructions[1].Title");
        content.Add(new StringContent("Serve."), "Instructions[1].Instruction");
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
    public async Task CreateRecipeDetails_RejectsNonPositiveIngredientIdInsteadOfDiscardingIt()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            ingredients: new[]
            {
                new RecipeIngredientFormItem(0, 100m, "g"),
                new RecipeIngredientFormItem(1, 100m, "g"),
            });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("ingredient ids", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_RejectsNonPositiveDietIdInsteadOfDiscardingIt()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            dietIds: new[] { 0, 1 });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("diet ids", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_NormalizesEquivalentIngredientUnit()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            ingredients: new[] { new RecipeIngredientFormItem(1, 100m, " G ") });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var created = await response.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(created);

        var storedUnit = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            """
            SELECT unit
            FROM recipe_ingredients
            WHERE recipe_id = @RecipeId AND ingredient_id = 1;
            """,
            new { RecipeId = created!.Id });

        Assert.Equal("g", storedUnit);
    }

    [Fact]
    public async Task CreateRecipeDetails_RejectsIngredientWithMissingCoreCalculationData()
    {
        await Factory.Database.ExecuteAsync(
            "DELETE FROM ingredient_nutrition WHERE ingredient_id = 1;");

        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            name: "Must Roll Back",
            ingredients: new[] { new RecipeIngredientFormItem(1, 100m, "g") });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("core calculation data", problem!.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM recipes WHERE name = 'Must Roll Back';"));
    }

    [Fact]
    public async Task CreateRecipeDetails_RejectsUnitMismatch()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            ingredients: new[] { new RecipeIngredientFormItem(1, 100m, "ml") });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("basis unit", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRecipeDetails_AcceptsZeroCoreValues()
    {
        await Factory.Database.ExecuteAsync(
            """
            UPDATE ingredient_nutrition
            SET price = 0, calories_kcal = 0, protein_g = 0, carbs_g = 0
            WHERE ingredient_id = 1;
            """);

        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            ingredients: new[] { new RecipeIngredientFormItem(1, 100m, "g") });

        var response = await client.PostAsync("/api/admin/recipe-details", content);
        var recipe = await response.Content.ReadFromJsonAsync<RecipeSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(recipe);
        Assert.Equal(0m, recipe!.CaloriesTotal);
        Assert.Equal(0m, recipe.ProteinTotal);
        Assert.Equal(0m, recipe.CarbsTotal);
        Assert.Equal(0m, recipe.EstimatedCostPerServing);
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
        Assert.Equal(247.80m, recipe.CaloriesTotal);
        Assert.Equal(8.52m, recipe.ProteinTotal);
        Assert.Equal(50.86m, recipe.CarbsTotal);
        Assert.Equal(0.80m, recipe.EstimatedCostPerServing);

        var getResponse = await client.GetAsync("/api/recipes/1");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("Updated Chicken Rice Bowl", detail!.Name);
        Assert.Equal("Hard", detail.Difficulty);
        Assert.Single(detail.Diets!);
        Assert.Equal(recipe.CaloriesTotal, detail.CaloriesTotal);
        Assert.Equal(recipe.ProteinTotal, detail.ProteinTotal);
        Assert.Equal(recipe.CarbsTotal, detail.CarbsTotal);
        Assert.Equal(recipe.EstimatedCostPerServing, detail.EstimatedCostPerServing);
    }

    [Fact]
    public async Task UpdateRecipeDetails_ClearsStaleCardImageWhenMainImageIsReplaced()
    {
        await Factory.Database.ExecuteAsync(
            "UPDATE recipes SET card_image_blob_name = 'dataset/recipes/REC-0001/card.avif' WHERE id = 1;");
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest();

        var response = await client.PutAsync("/api/admin/recipe-details/1", content);
        var cardBlobName = await Factory.Database.QuerySingleOrDefaultAsync<string>(
            "SELECT card_image_blob_name FROM recipes WHERE id = 1;");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(cardBlobName);
    }

    [Fact]
    public async Task UpdateRecipeDetails_PreservesReorderedInstructionSequence()
    {
        var expectedInstructions = new[]
        {
            new RecipeInstructionStepDto("Serve", "Serve the finished bowl."),
            new RecipeInstructionStepDto("Cook", "Cook rice and vegetables."),
            new RecipeInstructionStepDto("Season", "Season the chicken."),
        };
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            instructions: new[]
            {
                new RecipeInstructionFormItem(" Serve ", " Serve the finished bowl. "),
                new RecipeInstructionFormItem("Cook", "Cook rice and vegetables."),
                new RecipeInstructionFormItem(" Season ", " Season the chicken.  "),
            },
            includeImage: false);

        var updateResponse = await client.PutAsync("/api/admin/recipe-details/1", content);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/recipes/1");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(expectedInstructions, detail!.Instructions);
    }

    [Fact]
    public async Task UpdateRecipeDetails_RejectsWhenEveryInstructionIsFullyBlankAndKeepsExistingSequence()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;
        using var content = MultipartContentBuilder.CreateRecipeRequest(
            instructions: new[]
            {
                new RecipeInstructionFormItem(" ", "\t"),
                new RecipeInstructionFormItem("", "   "),
            },
            includeImage: false);

        var updateResponse = await client.PutAsync("/api/admin/recipe-details/1", content);
        var problem = await updateResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("complete instruction step", problem!.Detail, StringComparison.OrdinalIgnoreCase);

        var getResponse = await client.GetAsync("/api/recipes/1");
        var detail = await getResponse.Content.ReadFromJsonAsync<RecipeDetailDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(
            new[]
            {
                new RecipeInstructionStepDto("Season the chicken", "Season and sear the chicken."),
                new RecipeInstructionStepDto("Cook the sides", "Cook the rice and steam the broccoli."),
                new RecipeInstructionStepDto("Assemble the bowl", "Slice the chicken and serve everything together."),
            },
            detail!.Instructions);
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

    private sealed record RecipeTotalsRow(
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal,
        decimal EstimatedCostPerServing);
}
