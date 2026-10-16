using System.Text.Json.Serialization;

namespace Appetee.Application.Models.MealPlans;

/// <summary>Returns the captured transactional payload and its authoritative owner revision.</summary>
public sealed record MealPlanWriteResult<TPayload>(
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] long Revision,
    bool Changed,
    TPayload Payload);
