using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow), JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record MealTargetRequest
{
    private int? _mealId, _defaultSlot;
    public int? MealId { get => _mealId; init { _mealId=value; HasMealId=true; } }
    public int? DefaultSlot { get => _defaultSlot; init { _defaultSlot=value; HasDefaultSlot=true; } }
    internal bool HasMealId { get; private init; }
    internal bool HasDefaultSlot { get; private init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow), JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AddRecipeItemRequest
{
    public required MealTargetRequest Meal { get; init; }
    public required int RecipeId { get; init; }
    public required int Quantity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow), JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record SetRecipeQuantityRequest
{
    public required int Quantity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow), JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ManualItemRequest
{
    // Nullable at binding only: Application reports every omitted/null field by its exact path.
    public string? Name { get; init; }
    public decimal? CaloriesKcal { get; init; }
    public decimal? ProteinG { get; init; }
    public decimal? CarbsG { get; init; }
    public decimal? FatG { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow), JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AddManualItemRequest
{
    public required MealTargetRequest Meal { get; init; }
    public required ManualItemRequest ManualItem { get; init; }
}
