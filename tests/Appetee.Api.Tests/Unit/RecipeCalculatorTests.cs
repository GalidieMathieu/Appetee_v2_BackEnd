using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Api.Tests.Unit;

public sealed class RecipeCalculatorTests
{
    [Fact]
    public void BadgeValues_ExposeCanonicalVocabularyAndPriority()
    {
        Assert.Equal(
            [
                "High Protein",
                "Low Calorie",
                "Low Carb",
                "High Fiber",
                "Quick Meal",
                "Meal Prep",
                "Freezer Friendly",
                "Budget Friendly",
                "Few Ingredients",
            ],
            RecipeBadgeValues.All);
        Assert.Equal(
            ["High Protein", "Meal Prep", "Few Ingredients"],
            RecipeBadgeValues.Order(["Few Ingredients", "High Protein", "Meal Prep"]));
        Assert.False(RecipeBadgeValues.IsValid("high-protein"));
    }

    [Fact]
    public void Calculate_UsesAllIngredientsAndRoundsToPersistencePrecision()
    {
        var ingredients = new[]
        {
            new RecipeIngredientRequest { IngredientId = 1, Quantity = 220m, Unit = "g" },
            new RecipeIngredientRequest { IngredientId = 2, Quantity = 180m, Unit = "g" },
        };
        var data = new Dictionary<int, RecipeIngredientCalculationData>
        {
            [1] = new(1, "Chicken", 100m, "g", 2.40m, 165m, 31m, 0m),
            [2] = new(2, "Rice", 100m, "g", 0.65m, 123m, 2.70m, 25.60m),
        };

        var result = RecipeCalculator.Calculate(ingredients, data, servings: 3);

        Assert.Equal(584.40m, result.CaloriesTotal);
        Assert.Equal(73.06m, result.ProteinTotal);
        Assert.Equal(46.08m, result.CarbsTotal);
        Assert.Equal(194.80m, result.CaloriesPerServing);
        Assert.Equal(24.35m, result.ProteinPerServing);
        Assert.Equal(2.15m, result.EstimatedCostPerServing);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("price")]
    [InlineData("calories")]
    [InlineData("protein")]
    [InlineData("carbs")]
    public void Calculate_RejectsNegativeOrMissingCoreData(string invalidField)
    {
        var ingredient = new RecipeIngredientRequest { IngredientId = 1, Quantity = 100m, Unit = "g" };
        var data = new RecipeIngredientCalculationData(
            1,
            "Ingredient",
            invalidField == "basis" ? null : 100m,
            "g",
            invalidField == "price" ? -1m : 1m,
            invalidField == "calories" ? -1m : 1m,
            invalidField == "protein" ? null : 1m,
            invalidField == "carbs" ? -1m : 1m);

        var exception = Assert.Throws<ValidationException>(() =>
            RecipeCalculator.Calculate(new[] { ingredient }, new Dictionary<int, RecipeIngredientCalculationData> { [1] = data }, 1));

        Assert.Contains("core calculation data", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_RejectsUnitMismatch()
    {
        var ingredient = new RecipeIngredientRequest { IngredientId = 1, Quantity = 100m, Unit = "ml" };
        var data = new RecipeIngredientCalculationData(1, "Ingredient", 100m, "g", 1m, 1m, 1m, 1m);

        var exception = Assert.Throws<ValidationException>(() =>
            RecipeCalculator.Calculate(new[] { ingredient }, new Dictionary<int, RecipeIngredientCalculationData> { [1] = data }, 1));

        Assert.Contains("basis unit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
