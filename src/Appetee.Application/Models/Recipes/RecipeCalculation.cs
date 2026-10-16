using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Application.Models.Recipes;

public sealed record RecipeIngredientCalculationData(
    int Id,
    string Name,
    decimal? Basis,
    string? BasisUnit,
    decimal? Price,
    decimal? CaloriesKcal,
    decimal? ProteinG,
    decimal? FatG,
    decimal? CarbsG);

public sealed record RecipeCalculatedTotals(
    decimal CaloriesTotal,
    decimal ProteinTotal,
    decimal CarbsTotal,
    decimal? FatTotal,
    decimal CaloriesPerServing,
    decimal ProteinPerServing,
    decimal? FatPerServing,
    decimal EstimatedCostPerServing);

public static class RecipeCalculator
{
    private const decimal MaximumPersistedValue = 99_999_999.99m;

    public static RecipeCalculatedTotals Calculate(
        IReadOnlyList<RecipeIngredientRequest> ingredients,
        IReadOnlyDictionary<int, RecipeIngredientCalculationData> calculationData,
        int servings)
    {
        decimal calories = 0;
        decimal protein = 0;
        decimal carbs = 0;
        decimal fat = 0;
        decimal cost = 0;
        var isFatKnown = true;

        foreach (var ingredient in ingredients)
        {
            if (!calculationData.TryGetValue(ingredient.IngredientId, out var data))
                throw new ValidationException($"Invalid IngredientIds: {ingredient.IngredientId}");

            ValidateCalculationData(ingredient, data);

            var factor = ingredient.Quantity!.Value / data.Basis!.Value;
            calories += data.CaloriesKcal!.Value * factor;
            protein += data.ProteinG!.Value * factor;
            carbs += data.CarbsG!.Value * factor;
            if (data.FatG.HasValue)
                fat += data.FatG.Value * factor;
            else
                isFatKnown = false;
            cost += data.Price!.Value * factor;
        }

        var caloriesTotal = RoundAndValidate(calories, "calories total");
        var proteinTotal = RoundAndValidate(protein, "protein total");
        decimal? fatTotal = isFatKnown
            ? RoundAndValidate(fat, "fat total")
            : null;

        return new RecipeCalculatedTotals(
            caloriesTotal,
            proteinTotal,
            RoundAndValidate(carbs, "carbs total"),
            fatTotal,
            RoundAndValidate(caloriesTotal / servings, "calories per serving"),
            RoundAndValidate(proteinTotal / servings, "protein per serving"),
            fatTotal.HasValue
                ? RoundAndValidate(fatTotal.Value / servings, "fat per serving")
                : null,
            RoundAndValidate(cost / servings, "estimated cost per serving"));
    }

    private static void ValidateCalculationData(
        RecipeIngredientRequest ingredient,
        RecipeIngredientCalculationData data)
    {
        if (data.Basis is null || data.Basis <= 0 ||
            string.IsNullOrWhiteSpace(data.BasisUnit) ||
            data.Price is null || data.Price < 0 ||
            data.CaloriesKcal is null || data.CaloriesKcal < 0 ||
            data.ProteinG is null || data.ProteinG < 0 ||
            data.FatG < 0 ||
            data.CarbsG is null || data.CarbsG < 0)
        {
            throw new ValidationException(
                $"ingredient '{ingredient.IngredientId}' is missing valid core calculation data.");
        }

        if (!string.Equals(ingredient.Unit, data.BasisUnit, StringComparison.Ordinal))
        {
            throw new ValidationException(
                $"ingredient '{ingredient.IngredientId}' unit must match its basis unit '{data.BasisUnit}'.");
        }
    }

    private static decimal RoundAndValidate(decimal value, string field)
    {
        var rounded = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded > MaximumPersistedValue)
            throw new ValidationException($"calculated {field} exceeds the supported range.");

        return rounded;
    }
}
