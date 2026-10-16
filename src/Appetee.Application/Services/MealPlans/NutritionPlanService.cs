using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Services.MealPlans;

public sealed class NutritionPlanService(INutritionPlanQueries queries, INutritionPlanRepository repository,
    INutritionCalculationService calculator, TimeProvider timeProvider) : INutritionPlanService
{
    public async Task<NutritionPlanListDto> ListAsync(int currentUserId, CancellationToken ct)
    {
        var rows = await queries.ListAsync(currentUserId, ct);
        return new(rows.Context.Revision, NutritionPlanMapper.Settings(rows), rows.Plans.Select(NutritionPlanMapper.Summary).ToArray());
    }

    public async Task<NutritionPlanDetailDto> GetAsync(int currentUserId, int planId, CancellationToken ct)
    {
        if (planId <= 0) throw MealPlanRules.InvalidPlanField("planId", "Plan ID must be a positive integer.");
        var rows = await queries.GetAsync(currentUserId, planId, ct);
        var row = rows.Plans.SingleOrDefault() ?? throw NutritionPlanMapper.Unavailable();
        return new(rows.Context.Revision, NutritionPlanMapper.Settings(rows), NutritionPlanMapper.Detail(row),
            NutritionPlanMapper.ProfileDiffers(row, rows.Context));
    }

    public async Task<NutritionPlanMutationDto> CreateAsync(int currentUserId, CreateNutritionPlanRequest request,
        long expectedRevision, CancellationToken ct)
    {
        var name = MealPlanRules.NormalizePlanName(request.Name);
        if (request.Mode is not ("easy" or "intermediate"))
            throw MealPlanRules.InvalidPlanField("mode", "Choose easy or intermediate.");
        if (request.DefaultMealsPerDay is < MealPlanRules.MinDefaultMealsPerDay or > MealPlanRules.MaxDefaultMealsPerDay)
            throw MealPlanRules.InvalidPlanField("defaultMealsPerDay", "Default meals per day must be 1–8.");
        NutritionCalculationResult? calculation = null; string? policy = null; int? year = null;
        if (request.Mode == "easy")
        {
            if (request.HasCalculation || request.HasReviewedPolicyVersion || request.HasReviewedCalculationYear)
                throw MealPlanRules.InvalidPlanField("calculation", "Easy plans omit calculation and review fields.");
        }
        else
        {
            (calculation, policy, year) = CalculateReviewed(request.Calculation, request.ReviewedPolicyVersion,
                request.ReviewedCalculationYear, ct);
        }
        var result = await repository.CreateAsync(currentUserId,
            new NutritionPlanCreateData(name, request.Mode == "easy" ? "Easy" : "Intermediate",
                request.DefaultMealsPerDay, calculation, policy, year), expectedRevision, ct);
        var rows = result.Payload.Readback;
        return new(result.Revision, NutritionPlanMapper.Settings(rows), NutritionPlanMapper.Detail(rows.Plans.Single()),
            null, result.Payload.ProfileChanged ? NutritionPlanMapper.Profile(rows.Context) : null, result.Payload.PlannerInvalidated);
    }

    public async Task<NutritionPlanMutationDto> UpdateAsync(int currentUserId, int planId,
        UpdateNutritionPlanRequest request, long expectedRevision, CancellationToken ct)
    {
        ValidateId(planId);
        var name = MealPlanRules.NormalizePlanName(request.Name);
        if (request.DefaultMealsPerDay is < MealPlanRules.MinDefaultMealsPerDay or > MealPlanRules.MaxDefaultMealsPerDay)
            throw MealPlanRules.InvalidPlanField("defaultMealsPerDay", "Default meals per day must be 1–8.");
        NutritionCalculationResult? calculation = null; string? policy = null; int? year = null;
        if (request.HasCalculation || request.HasReviewedPolicyVersion || request.HasReviewedCalculationYear)
            (calculation, policy, year) = CalculateReviewed(request.Calculation, request.ReviewedPolicyVersion,
                request.ReviewedCalculationYear, ct);
        return Mutation(await repository.UpdateAsync(currentUserId, planId,
            new(name, request.DefaultMealsPerDay, calculation, policy, year), expectedRevision, ct));
    }

    public async Task<NutritionPlanMutationDto> DuplicateAsync(int currentUserId, int planId, long expectedRevision, CancellationToken ct)
    { ValidateId(planId); return Mutation(await repository.DuplicateAsync(currentUserId, planId, expectedRevision, ct)); }
    public async Task<NutritionPlanMutationDto> DeleteAsync(int currentUserId, int planId, long expectedRevision, CancellationToken ct)
    { ValidateId(planId); return Mutation(await repository.DeleteAsync(currentUserId, planId, expectedRevision, ct)); }
    public async Task<NutritionPlanMutationDto> SetCurrentAsync(int currentUserId, SetCurrentNutritionPlanRequest request,
        long expectedRevision, CancellationToken ct)
    {
        if (request.NutritionPlanId <= 0) throw MealPlanRules.InvalidPlanField("nutritionPlanId", "Plan ID must be a positive integer.");
        return Mutation(await repository.SetCurrentAsync(currentUserId, request.NutritionPlanId, expectedRevision, ct));
    }

    private static void ValidateId(int planId)
    { if (planId <= 0) throw MealPlanRules.InvalidPlanField("planId", "Plan ID must be a positive integer."); }

    private static NutritionPlanMutationDto Mutation(MealPlanWriteResult<NutritionPlanMutationRowData> result)
    {
        var payload = result.Payload; var rows = payload.Readback;
        return new(result.Revision, NutritionPlanMapper.Settings(rows),
            payload.DeletedPlanId.HasValue ? null : NutritionPlanMapper.Detail(rows.Plans.Single()), payload.DeletedPlanId,
            payload.ProfileChanged ? NutritionPlanMapper.Profile(rows.Context) : null, payload.PlannerInvalidated);
    }

    private (NutritionCalculationResult Calculation, string Policy, int Year) CalculateReviewed(
        NutritionCalculationRequest? request, string? reviewedPolicy, int? reviewedYear, CancellationToken ct)
    {
        if (request is null) throw MealPlanRules.InvalidPlanField("calculation", "Explicit calculation inputs are required.");
        if (string.IsNullOrEmpty(reviewedPolicy)) throw MealPlanRules.InvalidPlanField("reviewedPolicyVersion", "Reviewed policy version is required.");
        if (reviewedYear is null) throw MealPlanRules.InvalidPlanField("reviewedCalculationYear", "Reviewed calculation year is required.");
        if (reviewedPolicy != NutritionPolicy.Version || reviewedYear != timeProvider.GetUtcNow().UtcDateTime.Year)
            throw new ConflictException("The calculation preview is stale. Calculate and review again.", code: "calculation_preview_stale");
        NutritionCalculationDto computed;
        try { computed = calculator.Calculate(request, ct); }
        catch (ValidationException ex) when (ex.Errors is not null)
        { throw new ValidationException(ex.Message, ex.Code ?? "validation_failed", ex.Errors.ToDictionary(e => $"calculation.{e.Key}", e => e.Value)); }
        if (reviewedPolicy != computed.PolicyVersion || reviewedYear != computed.CalculationYear)
            throw new ConflictException("The calculation preview is stale. Calculate and review again.", code: "calculation_preview_stale");
        var i = computed.Inputs; var t = computed.Targets;
        return (new(new(i.CalculationSex, i.BirthYear, i.AgeYears, i.HeightCm, i.WeightKg,
                NutritionPolicy.ParseGoal(i.Goal)!.Value, NutritionPolicy.ParseActivity(i.ActivityLevel)!.Value),
            computed.RmrKcal, computed.MaintenanceKcal,
            new((int)t.CaloriesKcal!.Value, (int)t.ProteinG!.Value, (int)t.CarbsG!.Value, (int)t.FatG!.Value), computed.Warnings),
            computed.PolicyVersion, computed.CalculationYear);
    }
}
