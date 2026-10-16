using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateNutritionPlanRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Mode,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] int DefaultMealsPerDay)
{
    private NutritionCalculationRequest? _calculation;
    private string? _reviewedPolicyVersion;
    private int? _reviewedCalculationYear;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NutritionCalculationRequest? Calculation
    {
        get => _calculation;
        init { _calculation = value; HasCalculation = true; }
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewedPolicyVersion
    {
        get => _reviewedPolicyVersion;
        init { _reviewedPolicyVersion = value; HasReviewedPolicyVersion = true; }
    }
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReviewedCalculationYear
    {
        get => _reviewedCalculationYear;
        init { _reviewedCalculationYear = value; HasReviewedCalculationYear = true; }
    }
    internal bool HasCalculation { get; private init; }
    internal bool HasReviewedPolicyVersion { get; private init; }
    internal bool HasReviewedCalculationYear { get; private init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateNutritionPlanRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] int DefaultMealsPerDay)
{
    private NutritionCalculationRequest? _calculation;
    private string? _reviewedPolicyVersion;
    private int? _reviewedCalculationYear;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NutritionCalculationRequest? Calculation
    { get => _calculation; init { _calculation = value; HasCalculation = true; } }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewedPolicyVersion
    { get => _reviewedPolicyVersion; init { _reviewedPolicyVersion = value; HasReviewedPolicyVersion = true; } }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonNumberHandling(JsonNumberHandling.Strict)]
    public int? ReviewedCalculationYear
    { get => _reviewedCalculationYear; init { _reviewedCalculationYear = value; HasReviewedCalculationYear = true; } }
    internal bool HasCalculation { get; private init; }
    internal bool HasReviewedPolicyVersion { get; private init; }
    internal bool HasReviewedCalculationYear { get; private init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetCurrentNutritionPlanRequest(
    [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] int NutritionPlanId);
