namespace Appetee.Application.Dtos.MealPlans;

public sealed record NutrientsDto(decimal? CaloriesKcal, decimal? ProteinG, decimal? CarbsG, decimal? FatG);

public sealed record CalculationInputsDto(
    string CalculationSex, int BirthYear, int AgeYears, decimal HeightCm, decimal WeightKg,
    string Goal, string ActivityLevel);

public sealed record NutritionCalculationDto(
    string PolicyVersion, int CalculationYear, CalculationInputsDto Inputs,
    decimal RmrKcal, decimal MaintenanceKcal, NutrientsDto Targets, IReadOnlyList<string> Warnings);
