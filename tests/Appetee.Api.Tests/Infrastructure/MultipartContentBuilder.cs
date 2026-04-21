using System.Globalization;
using System.Net.Http.Headers;

namespace Appetee.Api.Tests.Infrastructure;

internal sealed record RecipeIngredientFormItem(int IngredientId, decimal Quantity, string Unit);

internal static class MultipartContentBuilder
{
    public static MultipartFormDataContent CreateIngredientRequest(
        string name = "Test Ingredient",
        decimal basis = 100m,
        decimal caloriesKcal = 210m,
        decimal price = 3.25m,
        bool includeImage = true,
        decimal? proteinG = 12m,
        decimal? fatG = 8m,
        decimal? carbsG = 5m,
        decimal? sugarG = 2m,
        decimal? fiberG = 1m,
        decimal? sodiumMg = 125m,
        decimal? vitaminCMg = 3m,
        decimal? ironMg = 1.5m)
    {
        var content = new MultipartFormDataContent();

        AddString(content, "Name", name);
        AddString(content, "Basis", basis);
        AddString(content, "CaloriesKcal", caloriesKcal);
        AddString(content, "Price", price);
        AddNullableString(content, "ProteinG", proteinG);
        AddNullableString(content, "FatG", fatG);
        AddNullableString(content, "CarbsG", carbsG);
        AddNullableString(content, "SugarG", sugarG);
        AddNullableString(content, "FiberG", fiberG);
        AddNullableString(content, "SodiumMg", sodiumMg);
        AddNullableString(content, "VitaminCMg", vitaminCMg);
        AddNullableString(content, "IronMg", ironMg);

        if (includeImage)
        {
            content.Add(CreateImageContent(), "Image", "ingredient.png");
        }

        return content;
    }

    public static MultipartFormDataContent CreateRecipeRequest(
        string name = "Sheet Pan Chicken",
        decimal caloriesTotal = 640m,
        decimal proteinTotal = 44m,
        decimal carbsTotal = 52m,
        string instructions = "Season the chicken.\nRoast everything together.\nServe warm.",
        int prepTimeMinutes = 35,
        int servings = 3,
        string difficulty = "Medium",
        IReadOnlyList<string>? badges = null,
        IReadOnlyList<int>? dietIds = null,
        decimal? estimatedCostPerServing = 7.10m,
        IReadOnlyList<RecipeIngredientFormItem>? ingredients = null,
        bool includeImage = true)
    {
        var content = new MultipartFormDataContent();

        AddString(content, "Name", name);
        AddString(content, "CaloriesTotal", caloriesTotal);
        AddString(content, "ProteinTotal", proteinTotal);
        AddString(content, "CarbsTotal", carbsTotal);
        AddString(content, "Instructions", instructions);
        AddString(content, "PrepTimeMinutes", prepTimeMinutes);
        AddString(content, "Servings", servings);
        AddString(content, "Difficulty", difficulty);

        if (estimatedCostPerServing is not null)
        {
            AddString(content, "EstimatedCostPerServing", estimatedCostPerServing.Value);
        }

        var badgeValues = badges ?? new[] { "high-protein" };
        for (var i = 0; i < badgeValues.Count; i++)
        {
            AddString(content, $"Badges[{i}]", badgeValues[i]);
        }

        var dietValues = dietIds ?? new[] { 2, 3 };
        for (var i = 0; i < dietValues.Count; i++)
        {
            AddString(content, $"DietIds[{i}]", dietValues[i]);
        }

        var ingredientValues = ingredients ?? new[]
        {
            new RecipeIngredientFormItem(1, 220m, "g"),
            new RecipeIngredientFormItem(2, 180m, "g"),
        };

        for (var i = 0; i < ingredientValues.Count; i++)
        {
            var ingredient = ingredientValues[i];
            AddString(content, $"Ingredients[{i}].IngredientId", ingredient.IngredientId);
            AddString(content, $"Ingredients[{i}].Quantity", ingredient.Quantity);
            AddString(content, $"Ingredients[{i}].Unit", ingredient.Unit);
        }

        if (includeImage)
        {
            content.Add(CreateImageContent(), "Image", "recipe.png");
        }

        return content;
    }

    private static void AddString(MultipartFormDataContent content, string name, object value) =>
        content.Add(new StringContent(Convert.ToString(value, CultureInfo.InvariantCulture)!), name);

    private static void AddNullableString(MultipartFormDataContent content, string name, decimal? value)
    {
        if (value is not null)
        {
            AddString(content, name, value.Value);
        }
    }

    private static ByteArrayContent CreateImageContent()
    {
        var image = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5, 6 });
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return image;
    }
}
