using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NutritionCalculationRequest(
    [property: JsonRequired] NutritionProfileRequest Profile,
    [property: JsonRequired] string Goal,
    [property: JsonRequired] string ActivityLevel);
