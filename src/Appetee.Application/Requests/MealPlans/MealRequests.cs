using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record CreateMealRequest
{
    public required string Name { get; init; }
    private int? _defaultSlot;
    public int? DefaultSlot
    {
        get => _defaultSlot;
        init { _defaultSlot=value; HasDefaultSlot=true; }
    }
    internal bool HasDefaultSlot { get; private init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenameMealRequest
{
    public required string Name { get; init; }
}
