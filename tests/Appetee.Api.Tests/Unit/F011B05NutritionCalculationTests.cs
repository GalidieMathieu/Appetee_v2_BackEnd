using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.Services.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B05NutritionCalculationTests
{
    private static NutritionCalculationInputs Input(string sex = "male", int age = 28,
        NutritionGoal goal = NutritionGoal.Maintain, NutritionActivity activity = NutritionActivity.Active) =>
        new(sex, 2026 - age, age, 180m, 80m, goal, activity);

    [Theory]
    [InlineData("male", "performance", "inactive", 2800, 128, 379, 86)]
    [InlineData("male", "performance", "lowActive", 3200, 128, 452, 98)]
    [InlineData("male", "performance", "active", 3500, 128, 506, 107)]
    [InlineData("male", "performance", "veryActive", 4100, 128, 616, 125)]
    [InlineData("male", "maintain", "inactive", 2800, 128, 379, 86)]
    [InlineData("male", "maintain", "lowActive", 3200, 128, 452, 98)]
    [InlineData("male", "maintain", "active", 3500, 128, 506, 107)]
    [InlineData("male", "maintain", "veryActive", 4100, 128, 616, 125)]
    [InlineData("male", "fatLoss", "inactive", 2375, 160, 270, 73)]
    [InlineData("male", "fatLoss", "lowActive", 2725, 160, 335, 83)]
    [InlineData("male", "fatLoss", "active", 3000, 160, 401, 84)]
    [InlineData("male", "fatLoss", "veryActive", 3600, 160, 493, 110)]
    [InlineData("male", "muscleGain", "inactive", 2950, 128, 407, 90)]
    [InlineData("male", "muscleGain", "lowActive", 3375, 128, 484, 103)]
    [InlineData("male", "muscleGain", "active", 3675, 128, 539, 112)]
    [InlineData("male", "muscleGain", "veryActive", 4325, 128, 656, 132)]
    [InlineData("male", "weightGain", "inactive", 3100, 128, 433, 95)]
    [InlineData("male", "weightGain", "lowActive", 3525, 128, 510, 108)]
    [InlineData("male", "weightGain", "active", 3850, 128, 569, 118)]
    [InlineData("male", "weightGain", "veryActive", 4525, 128, 693, 138)]
    [InlineData("male", "recomposition", "inactive", 2800, 128, 379, 86)]
    [InlineData("male", "recomposition", "lowActive", 3200, 128, 452, 98)]
    [InlineData("male", "recomposition", "active", 3500, 128, 506, 107)]
    [InlineData("male", "recomposition", "veryActive", 4100, 128, 616, 125)]
    [InlineData("female", "performance", "inactive", 2550, 128, 334, 78)]
    [InlineData("female", "performance", "lowActive", 2900, 128, 397, 89)]
    [InlineData("female", "performance", "active", 3175, 128, 448, 97)]
    [InlineData("female", "performance", "veryActive", 3725, 128, 547, 114)]
    [InlineData("female", "maintain", "inactive", 2550, 128, 334, 78)]
    [InlineData("female", "maintain", "lowActive", 2900, 128, 397, 89)]
    [InlineData("female", "maintain", "active", 3175, 128, 448, 97)]
    [InlineData("female", "maintain", "veryActive", 3725, 128, 547, 114)]
    [InlineData("female", "fatLoss", "inactive", 2150, 160, 240, 61)]
    [InlineData("female", "fatLoss", "lowActive", 2475, 160, 319, 62)]
    [InlineData("female", "fatLoss", "active", 2700, 160, 380, 60)]
    [InlineData("female", "fatLoss", "veryActive", 3225, 160, 480, 74)]
    [InlineData("female", "muscleGain", "inactive", 2675, 128, 356, 82)]
    [InlineData("female", "muscleGain", "lowActive", 3050, 128, 425, 93)]
    [InlineData("female", "muscleGain", "active", 3325, 128, 474, 102)]
    [InlineData("female", "muscleGain", "veryActive", 3900, 128, 579, 119)]
    [InlineData("female", "weightGain", "inactive", 2800, 128, 379, 86)]
    [InlineData("female", "weightGain", "lowActive", 3200, 128, 452, 98)]
    [InlineData("female", "weightGain", "active", 3500, 128, 506, 107)]
    [InlineData("female", "weightGain", "veryActive", 4100, 128, 616, 125)]
    [InlineData("female", "recomposition", "inactive", 2550, 128, 334, 78)]
    [InlineData("female", "recomposition", "lowActive", 2900, 128, 397, 89)]
    [InlineData("female", "recomposition", "active", 3175, 128, 448, 97)]
    [InlineData("female", "recomposition", "veryActive", 3725, 128, 547, 114)]
    public void AllGoalsAndActivities_MatchFixedVectorsForBothSexCoefficients(
        string sex, string goal, string activity, int energy, int protein, int carbs, int fat)
    {
        var result = NutritionCalculator.Calculate(Input(sex, goal: NutritionPolicy.ParseGoal(goal)!.Value,
            activity: NutritionPolicy.ParseActivity(activity)!.Value));
        Assert.Equal(new NutritionTargets(energy, protein, carbs, fat), result.Targets);
        AssertInvariants(result.Targets);
    }

    [Fact]
    public void Golden_RetainsFullDecimalEstimatesAndConvertsCentimetresToMetres()
    {
        var male = NutritionCalculator.Calculate(Input());
        var female = NutritionCalculator.Calculate(Input("female"));
        Assert.Equal(2005.4844m, male.RmrKcal);
        Assert.Equal(3509.5977m, male.MaintenanceKcal);
        Assert.Equal(191.027m, male.RmrKcal - female.RmrKcal);
        Assert.Equal(587.728m, NutritionCalculator.Calculate(Input() with { HeightCm = 230m }).RmrKcal
            - NutritionCalculator.Calculate(Input() with { HeightCm = 130m }).RmrKcal);
    }

    [Theory]
    [InlineData(1512.49, 1500)]
    [InlineData(1512.50, 1525)]
    [InlineData(1487.50, 1500)]
    [InlineData(1487.49, 1475)]
    [InlineData(6012.49, 6000)]
    [InlineData(6012.50, 6025)]
    public void EnergyRounding_UsesExactMidpointAwayFromZero(decimal raw, int expected) =>
        Assert.Equal(expected, NutritionPolicy.RoundEnergy(raw));

    [Fact]
    public void EstimatesAndMacroMidpoints_RoundAwayFromZeroWithoutPrematureRounding()
    {
        Assert.Equal(1000.01m, NutritionPolicy.RoundEstimate(1000.005m));
        Assert.Equal(1000.02m, NutritionPolicy.RoundEstimate(1000.015m));
        Assert.Equal(83, NutritionCalculator.CalculateTargets(2700, 35m,
            NutritionGoal.Maintain, NutritionActivity.Active).FatG); // centre = 82.5
        Assert.Equal(452, NutritionCalculator.CalculateTargets(3200, 80m,
            NutritionGoal.Maintain, NutritionActivity.LowActive).CarbsG); // residual = 451.5
        var service = new NutritionCalculationService(new FixedClock(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)));
        var dto = service.Calculate(new(new("male", 1998, 180m, 80m), "maintain", "active"));
        Assert.Equal(2005.48m, dto.RmrKcal);
        Assert.Equal(3509.60m, dto.MaintenanceKcal);
        Assert.Equal(new NutrientsDto(3500, 128, 506, 107), dto.Targets);
        Assert.Equal("mp-v1.0.0-2026-10", dto.PolicyVersion);
    }

    [Theory]
    [InlineData(75, 202.59, 3650, true)]
    [InlineData(75.01, 132.08, 2900, false)]
    public void EarlyEstimateRounding_WouldChooseWrongEnergyButServiceUsesFullPrecision(
        decimal weight, decimal height, int expectedEnergy, bool roundRmr)
    {
        var input = Input() with { WeightKg = weight, HeightCm = height };
        var calculation = NutritionCalculator.Calculate(input);
        var roundedEarly = roundRmr
            ? NutritionPolicy.RoundEstimate(calculation.RmrKcal) * 1.75m
            : NutritionPolicy.RoundEstimate(calculation.MaintenanceKcal);
        Assert.NotEqual(expectedEnergy, NutritionPolicy.RoundEnergy(roundedEarly));
        Assert.Equal(expectedEnergy, calculation.Targets.CaloriesKcal);
        var service = new NutritionCalculationService(new FixedClock(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)));
        var dto = service.Calculate(new(new("male", 1998, height, weight), "maintain", "active"));
        Assert.Equal((decimal)expectedEnergy, dto.Targets.CaloriesKcal);
    }

    [Theory]
    [InlineData(2000, 1700, 2200)]
    [InlineData(4000, 3500, 4400)]
    [InlineData(6000, 5500, 6500)]
    public void GoalAdjustments_RespectPercentageAndAbsoluteCaps(decimal maintenance, decimal loss, decimal gain)
    {
        Assert.Equal(loss, NutritionPolicy.GoalEnergy(maintenance, NutritionGoal.FatLoss));
        Assert.Equal(gain, NutritionPolicy.GoalEnergy(maintenance, NutritionGoal.WeightGain));
        Assert.Equal(maintenance * 1.05m, NutritionPolicy.GoalEnergy(maintenance, NutritionGoal.MuscleGain));
    }

    [Theory]
    [InlineData(1500)]
    [InlineData(6000)]
    public void SupportedEnergyEndpoints_AreInclusive(int energy) =>
        AssertInvariants(NutritionCalculator.CalculateTargets(energy, 50.01m, NutritionGoal.Maintain, NutritionActivity.Active));

    [Theory]
    [InlineData(1475)]
    [InlineData(6025)]
    public void OutsideSupportedEnergy_IsRejectedNotClamped(int energy) =>
        Assert.Equal("calculation_outside_supported_range", Assert.Throws<ValidationException>(() =>
            NutritionCalculator.CalculateTargets(energy, 80m, NutritionGoal.Maintain, NutritionActivity.Active)).Code);

    [Theory]
    [InlineData(1500, 150)] // equality: 4P + 20%E == E
    [InlineData(1500, 250)]
    public void ImpossibleProteinAndMinimumFat_ReturnFeasibilityError(int energy, decimal weight) =>
        Assert.Equal("macro_feasibility_conflict", Assert.Throws<ValidationException>(() =>
            NutritionCalculator.CalculateTargets(energy, weight, NutritionGoal.FatLoss, NutritionActivity.Active)).Code);

    [Fact]
    public void ProteinIsCeilingAndFatClampsToMinimumWhenCarbPreferenceCannotFit()
    {
        var result = NutritionCalculator.CalculateTargets(2700, 80.01m, NutritionGoal.FatLoss, NutritionActivity.Active);
        Assert.Equal(161, result.ProteinG);
        Assert.Equal(60, result.FatG);
        AssertInvariants(result);
    }

    [Theory]
    [InlineData(35, "active", false)]
    [InlineData(36, "active", true)]
    [InlineData(18, "inactive", true)]
    public void PopulationWarning_IsStrictlyAgeOver35OrInactive(int age, string activity, bool expected)
    {
        var result = NutritionCalculator.Calculate(Input(age: age, activity: NutritionPolicy.ParseActivity(activity)!.Value));
        Assert.Equal(expected, result.Warnings.Contains(NutritionPolicy.PopulationWarning));
    }

    [Theory]
    [InlineData(399, 400, false)]
    [InlineData(398, 400, true)]
    [InlineData(399, 400.01, true)]
    public void CarbohydrateWarning_IsStrictlyBelowPreferredMinusOne(int actual, decimal preferred, bool expected) =>
        Assert.Equal(expected, NutritionPolicy.CarbohydrateBelowPreferred(actual, preferred));

    [Fact]
    public void WarningIsNonblockingAndProtectsProtein()
    {
        var result = NutritionCalculator.Calculate(Input("female", goal: NutritionGoal.FatLoss));
        Assert.Equal(new NutritionTargets(2700, 160, 380, 60), result.Targets);
        Assert.Contains(NutritionPolicy.CarbohydrateWarning, result.Warnings);
    }

    [Fact]
    public void BoundedGrid_RechecksEnergyFatAndPositiveIntegerPostconditions()
    {
        var accepted = 0; var rejected = 0;
        foreach (var sex in new[] { "male", "female" })
        foreach (var age in new[] { 18, 28, 35, 36, 65 })
        for (var height = 120; height <= 230; height += 10)
        for (var weight = 35; weight <= 250; weight += 5)
        foreach (var goal in Enum.GetValues<NutritionGoal>())
        foreach (var activity in Enum.GetValues<NutritionActivity>())
        {
            try
            {
                var result = NutritionCalculator.Calculate(Input(sex, age, goal, activity) with { HeightCm = height, WeightKg = weight });
                AssertInvariants(result.Targets); accepted++;
            }
            catch (ValidationException ex)
            {
                Assert.Equal("calculation_outside_supported_range", ex.Code); rejected++;
            }
        }
        Assert.True(accepted > 50000); Assert.True(rejected > 0);
        Assert.Equal(126720, accepted + rejected);
    }

    [Fact]
    public void ServiceUsesUtcClockAndEligibilityNotClientAge()
    {
        var clock = new FixedClock(new DateTimeOffset(2029, 12, 31, 17, 0, 0, TimeSpan.FromHours(-7)));
        var service = new NutritionCalculationService(clock);
        var request = new NutritionCalculationRequest(new("male", 2012, 180m, 80m), "maintain", "active");
        var error = Assert.Throws<ValidationException>(() => service.Calculate(request));
        Assert.Contains("profile.adultConfirmed", error.Errors!.Keys);
        var result = service.Calculate(request with { Profile = request.Profile with { AdultConfirmed = true } });
        Assert.Equal(2030, result.CalculationYear); Assert.Equal(18, result.Inputs.AgeYears);
        clock.Now = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var next = service.Calculate(request);
        Assert.Equal(2031, next.CalculationYear); Assert.Equal(19, next.Inputs.AgeYears);
        Assert.NotEqual(result.RmrKcal, next.RmrKcal);
        Assert.Equal(result.PolicyVersion, next.PolicyVersion);
    }

    [Theory]
    [InlineData(1961, true)]
    [InlineData(1960, false)]
    [InlineData(2009, false)]
    [InlineData(1900, false)]
    [InlineData(2147483647, false)]
    public void AgeBoundaries_AreAuthoritative(int birthYear, bool accepted)
    {
        var service = new NutritionCalculationService(new FixedClock(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)));
        var request = new NutritionCalculationRequest(new("male", birthYear, 180m, 80m, true), "maintain", "active");
        if (accepted) Assert.Equal(65, service.Calculate(request).Inputs.AgeYears);
        else Assert.Contains("profile.birthYear", Assert.Throws<ValidationException>(() => service.Calculate(request)).Errors!.Keys);
    }

    [Fact]
    public void ServiceHasOnlyClockDependencyAndHonoursCancellation()
    {
        Assert.Equal(typeof(TimeProvider), Assert.Single(Assert.Single(typeof(NutritionCalculationService)
            .GetConstructors()).GetParameters()).ParameterType);
        var service = new NutritionCalculationService(TimeProvider.System);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.Calculate(new(new("male", 1998, 180m, 80m), "maintain", "active"), cancel.Token));
    }

    private static void AssertInvariants(NutritionTargets targets)
    {
        Assert.InRange(targets.CaloriesKcal, 1500, 6000); Assert.Equal(0, targets.CaloriesKcal % 25);
        Assert.True(targets.ProteinG > 0 && targets.CarbsG > 0 && targets.FatG > 0);
        Assert.InRange(9m * targets.FatG / targets.CaloriesKcal, .20m, .35m);
        Assert.InRange(Math.Abs(4m * targets.ProteinG + 4m * targets.CarbsG + 9m * targets.FatG - targets.CaloriesKcal), 0m, 2m);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
