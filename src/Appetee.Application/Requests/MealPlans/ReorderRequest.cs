using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow),JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record ReorderRequest
{
    public required int[] OrderedIds {get;init;}
}
