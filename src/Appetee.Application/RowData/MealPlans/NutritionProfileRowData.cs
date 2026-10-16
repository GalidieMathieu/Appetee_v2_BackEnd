namespace Appetee.Application.RowData.MealPlans;

public sealed record NutritionProfileRowData
{
    public long Revision { get; init; }
    public string? CalculationSex { get; init; }
    public int? BirthYear { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? WeightKg { get; init; }
}
