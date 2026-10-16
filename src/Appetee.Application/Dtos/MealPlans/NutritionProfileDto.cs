using System.Text.Json.Serialization;

namespace Appetee.Application.Dtos.MealPlans;

public sealed record NutritionProfileValue(string CalculationSex, int BirthYear, decimal HeightCm, decimal WeightKg);

public sealed record NutritionProfileDto(
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Revision,
    NutritionProfileValue? Profile);
