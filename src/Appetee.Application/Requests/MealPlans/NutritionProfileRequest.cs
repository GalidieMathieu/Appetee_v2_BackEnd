using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NutritionProfileRequest(
    [property: JsonRequired] string CalculationSex,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] int BirthYear,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] decimal HeightCm,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] decimal WeightKg,
    bool AdultConfirmed = false);
