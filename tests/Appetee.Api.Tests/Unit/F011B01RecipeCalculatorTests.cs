using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B01RecipeCalculatorTests
{
    [Fact]
    public void Calculate_ScalesBasis100FatAt150And250AndRoundsPerServing()
    {
        var ingredients = new[]
        {
            Ingredient(1, 150m, "g"),
            Ingredient(2, 250m, "g"),
        };
        var data = new Dictionary<int, RecipeIngredientCalculationData>
        {
            [1] = CalculationData(1, fatG: 10m),
            [2] = CalculationData(2, fatG: 20m),
        };

        var result = RecipeCalculator.Calculate(ingredients, data, servings: 3);

        Assert.Equal(65m, result.FatTotal);
        Assert.Equal(21.67m, result.FatPerServing);
    }

    [Fact]
    public void Calculate_PreservesKnownZeroFat()
    {
        var result = RecipeCalculator.Calculate(
            [Ingredient(1, 150m, "g")],
            new Dictionary<int, RecipeIngredientCalculationData>
            {
                [1] = CalculationData(1, fatG: 0m),
            },
            servings: 2);

        Assert.Equal(0m, result.FatTotal);
        Assert.Equal(0m, result.FatPerServing);
    }

    [Fact]
    public void Calculate_PropagatesMissingFatWithoutChangingKnownCoreNutrition()
    {
        var result = RecipeCalculator.Calculate(
            [Ingredient(1, 150m, "g"), Ingredient(2, 250m, "g")],
            new Dictionary<int, RecipeIngredientCalculationData>
            {
                [1] = CalculationData(1, fatG: 10m),
                [2] = CalculationData(2, fatG: null),
            },
            servings: 2);

        Assert.Null(result.FatTotal);
        Assert.Null(result.FatPerServing);
        Assert.Equal(4m, result.CaloriesTotal);
        Assert.Equal(4m, result.ProteinTotal);
        Assert.Equal(4m, result.CarbsTotal);
    }

    [Fact]
    public void Calculate_RejectsIncompatibleUnit()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            RecipeCalculator.Calculate(
                [Ingredient(1, 150m, "ml")],
                new Dictionary<int, RecipeIngredientCalculationData>
                {
                    [1] = CalculationData(1, fatG: 10m),
                },
                servings: 1));

        Assert.Contains("basis unit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_RejectsFatOverflow()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            RecipeCalculator.Calculate(
                [Ingredient(1, 2m, "g")],
                new Dictionary<int, RecipeIngredientCalculationData>
                {
                    [1] = new(
                        1,
                        "Ingredient 1",
                        1m,
                        "g",
                        0m,
                        0m,
                        0m,
                        99_999_999.99m,
                        0m),
                },
                servings: 1));

        Assert.Contains("fat total", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supported range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RecipeIngredientRequest Ingredient(int id, decimal quantity, string unit) =>
        new() { IngredientId = id, Quantity = quantity, Unit = unit };

    private static RecipeIngredientCalculationData CalculationData(int id, decimal? fatG) =>
        new(
            id,
            $"Ingredient {id}",
            100m,
            "g",
            1m,
            1m,
            1m,
            fatG,
            1m);
}
