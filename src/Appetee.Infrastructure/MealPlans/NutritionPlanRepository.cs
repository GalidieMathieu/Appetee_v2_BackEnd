using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;
using Appetee.Infrastructure.Users;
using Appetee.Application.Services.MealPlans;
using MySqlConnector;

namespace Appetee.Infrastructure.MealPlans;

internal sealed class NutritionPlanRepository(MealPlanTransaction transactions) : INutritionPlanRepository
{
    public Task<MealPlanWriteResult<NutritionPlanMutationRowData>> UpdateAsync(int currentUserId, int planId,
        NutritionPlanUpdateData data, long expectedRevision, CancellationToken ct)
    {
        NutritionPlanRowData source = null!;
        var profileChanged = false; var invalidated = false;
        return transactions.ExecuteAsync("nutrition-plan.update", currentUserId, expectedRevision, async context =>
        {
            profileChanged = false; invalidated = false;
            if (data.Calculation is not null && source.Mode == "Easy")
                throw MealPlanRules.InvalidPlanField("calculation", "Easy plans omit calculation and review fields.");
            if (data.Calculation is not null && (data.CalculationYear != context.TimestampUtc.Year || data.PolicyVersion != NutritionPolicy.Version))
                throw new ConflictException("The calculation preview is stale. Calculate and review again.", code: "calculation_preview_stale");
            if (await context.QuerySingleAsync<bool>(NutritionPlanSql.NameExistsOther,
                new { CurrentUserId = currentUserId, PlanId = planId, data.Name })) throw NameConflict();
            var changed = source.Name != data.Name || source.DefaultMealsPerDay != data.DefaultMealsPerDay || data.Calculation is not null;
            if (!changed) return MealPlanMutation<NutritionPlanMutationRowData>.UnchangedResult(Pending());
            var current = await context.QuerySingleOrDefaultAsync<int?>(NutritionPlanSql.CurrentId, new { CurrentUserId = currentUserId });
            var t = data.Calculation?.Targets;
            invalidated = current == planId && (source.DefaultMealsPerDay != data.DefaultMealsPerDay ||
                (t is not null && (source.TargetCaloriesKcal != t.CaloriesKcal || source.TargetProteinG != t.ProteinG
                    || source.TargetCarbsG != t.CarbsG || source.TargetFatG != t.FatG)));
            try
            {
                if (data.Calculation is null)
                    await context.ExecuteAsync(NutritionPlanSql.UpdateMetadata,
                        new { CurrentUserId = currentUserId, PlanId = planId, data.Name, data.DefaultMealsPerDay, context.TimestampUtc });
                else
                {
                    var i = data.Calculation.Inputs;
                    var profile = await context.QuerySingleAsync<NutritionProfileRowData>(UserSql.GetNutritionProfile, new { CurrentUserId = currentUserId });
                    profileChanged = profile.CalculationSex != i.CalculationSex || profile.BirthYear != i.BirthYear
                        || profile.HeightCm != i.HeightCm || profile.WeightKg != i.WeightKg;
                    if (profileChanged) await context.ExecuteAsync(UserSql.UpdateNutritionProfile,
                        new { CurrentUserId = currentUserId, i.CalculationSex, i.BirthYear, i.HeightCm, i.WeightKg });
                    await context.ExecuteAsync(NutritionPlanSql.UpdateCalculation, new
                    {
                        CurrentUserId = currentUserId, PlanId = planId, data.Name, data.DefaultMealsPerDay,
                        Goal = NutritionPolicy.Code(i.Goal), ActivityLevel = NutritionPolicy.Code(i.ActivityLevel),
                        i.CalculationSex, i.BirthYear, i.AgeYears, i.HeightCm, i.WeightKg, data.PolicyVersion,
                        data.Calculation.RmrKcal, data.Calculation.MaintenanceKcal,
                        CaloriesKcal = t!.CaloriesKcal, t.ProteinG, t.CarbsG, t.FatG, context.TimestampUtc,
                    });
                }
            }
            catch (MySqlException ex) when (ex.Number == 1062) { throw NameConflict(); }
            return MealPlanMutation<NutritionPlanMutationRowData>.ChangedResult(Pending());
        }, ct, validateOwnership: async context => source = await LoadOwnedAsync(context, currentUserId, planId),
        readback: async (context, _) => new(await NutritionPlanReader.ReadAsync(context, currentUserId, planId), null, profileChanged, invalidated));
    }

    public Task<MealPlanWriteResult<NutritionPlanMutationRowData>> DuplicateAsync(int currentUserId, int planId,
        long expectedRevision, CancellationToken ct)
    {
        NutritionPlanRowData source = null!; var createdId = 0;
        return transactions.ExecuteAsync("nutrition-plan.duplicate", currentUserId, expectedRevision, async context =>
        {
            if (await context.QuerySingleAsync<int>(NutritionPlanSql.Count, new { CurrentUserId = currentUserId }) >= MealPlanRules.MaxPlansPerUser)
                throw new ConflictException("The nutrition plan limit has been reached.", code: "plan_limit_reached");
            string? name = null;
            for (var number = 1; number <= MealPlanRules.MaxPlansPerUser + 1; number++)
            {
                var candidate = MealPlanRules.DuplicatePlanName(source.Name, number);
                if (!await context.QuerySingleAsync<bool>(NutritionPlanSql.NameExists, new { CurrentUserId = currentUserId, Name = candidate }))
                { name = candidate; break; }
            }
            if (name is null) throw NameConflict();
            try { createdId = await context.QuerySingleAsync<int>(NutritionPlanSql.Duplicate,
                new { CurrentUserId = currentUserId, PlanId = planId, Name = name, context.TimestampUtc }); }
            catch (MySqlException ex) when (ex.Number == 1062) { throw NameConflict(); }
            return MealPlanMutation<NutritionPlanMutationRowData>.ChangedResult(Pending());
        }, ct, validateOwnership: async context => source = await LoadOwnedAsync(context, currentUserId, planId), isCopy: true,
        readback: async (context, _) => new(await NutritionPlanReader.ReadAsync(context, currentUserId, createdId), null, false, false));
    }

    public Task<MealPlanWriteResult<NutritionPlanMutationRowData>> DeleteAsync(int currentUserId, int planId,
        long expectedRevision, CancellationToken ct)
    {
        var invalidated = false;
        return transactions.ExecuteAsync("nutrition-plan.delete", currentUserId, expectedRevision, async context =>
        {
            invalidated = await context.ExecuteAsync(NutritionPlanSql.ClearCurrent, new { CurrentUserId = currentUserId, PlanId = planId }) == 1;
            await context.ExecuteAsync(NutritionPlanSql.Delete, new { CurrentUserId = currentUserId, PlanId = planId });
            return MealPlanMutation<NutritionPlanMutationRowData>.ChangedResult(Pending());
        }, ct, validateOwnership: async context => { _ = await LoadOwnedAsync(context, currentUserId, planId); },
        readback: async (context, _) => new(await NutritionPlanReader.ReadAsync(context, currentUserId, planId), planId, false, invalidated));
    }

    public Task<MealPlanWriteResult<NutritionPlanMutationRowData>> SetCurrentAsync(int currentUserId, int planId,
        long expectedRevision, CancellationToken ct)
    {
        var invalidated = false;
        return transactions.ExecuteAsync("nutrition-plan.set-current", currentUserId, expectedRevision, async context =>
        {
            var previous = await context.QuerySingleOrDefaultAsync<int?>(NutritionPlanSql.CurrentId, new { CurrentUserId = currentUserId });
            invalidated = previous != planId;
            if (!invalidated) return MealPlanMutation<NutritionPlanMutationRowData>.UnchangedResult(Pending());
            await context.ExecuteAsync(NutritionPlanSql.SetCurrent, new { CurrentUserId = currentUserId, PlanId = planId });
            return MealPlanMutation<NutritionPlanMutationRowData>.ChangedResult(Pending());
        }, ct, validateOwnership: async context => { _ = await LoadOwnedAsync(context, currentUserId, planId); },
        readback: async (context, _) => new(await NutritionPlanReader.ReadAsync(context, currentUserId, planId), null, false, invalidated));
    }

    private static async Task<NutritionPlanRowData> LoadOwnedAsync(MealPlanWriteContext context, int owner, int id) =>
        await context.QuerySingleOrDefaultAsync<NutritionPlanRowData>(NutritionPlanSql.Detail, new { CurrentUserId = owner, PlanId = id })
            ?? throw NutritionPlanMapper.Unavailable();
    private static ConflictException NameConflict() => new("A nutrition plan with this name already exists.", code: "plan_name_conflict");
    private static NutritionPlanMutationRowData Pending() => new(new(new(), null, []), null, false, false);

    public Task<MealPlanWriteResult<NutritionPlanCreatedRowData>> CreateAsync(int currentUserId,
        NutritionPlanCreateData data, long expectedRevision, CancellationToken ct)
    {
        var profileChanged = false; var firstCurrent = false; var planId = 0;
        return transactions.ExecuteAsync("nutrition-plan.create", currentUserId, expectedRevision,
            async context =>
            {
                if (data.Calculation is not null && (context.TimestampUtc.Year != data.CalculationYear
                    || data.PolicyVersion != NutritionPolicy.Version))
                    throw new ConflictException("The calculation preview is stale. Calculate and review again.", code: "calculation_preview_stale");
                if (await context.QuerySingleAsync<int>(NutritionPlanSql.Count, new { CurrentUserId = currentUserId }) >= MealPlanRules.MaxPlansPerUser)
                    throw new ConflictException("The nutrition plan limit has been reached.", code: "plan_limit_reached");
                if (await context.QuerySingleAsync<bool>(NutritionPlanSql.NameExists, new { CurrentUserId = currentUserId, data.Name }))
                    throw new ConflictException("A nutrition plan with this name already exists.", code: "plan_name_conflict");
                profileChanged = false;
                var calculation = data.Calculation; var i = calculation?.Inputs; var targets = calculation?.Targets;
                if (i is not null)
                {
                    var profile = await context.QuerySingleAsync<NutritionProfileRowData>(UserSql.GetNutritionProfile,
                        new { CurrentUserId = currentUserId });
                    profileChanged = profile.CalculationSex != i.CalculationSex || profile.BirthYear != i.BirthYear
                        || profile.HeightCm != i.HeightCm || profile.WeightKg != i.WeightKg;
                    if (profileChanged) await context.ExecuteAsync(UserSql.UpdateNutritionProfile, new
                    { CurrentUserId = currentUserId, i.CalculationSex, i.BirthYear, i.HeightCm, i.WeightKg });
                }
                var parameters = new
                {
                    CurrentUserId = currentUserId, data.Name, data.Mode, data.DefaultMealsPerDay,
                    Goal = i is null ? null : NutritionPolicy.Code(i.Goal),
                    ActivityLevel = i is null ? null : NutritionPolicy.Code(i.ActivityLevel),
                    CalculationSex = i?.CalculationSex, BirthYear = i?.BirthYear, AgeYears = i?.AgeYears,
                    HeightCm = i?.HeightCm, WeightKg = i?.WeightKg,
                    data.PolicyVersion, RmrKcal = calculation?.RmrKcal, MaintenanceKcal = calculation?.MaintenanceKcal,
                    CaloriesKcal = targets?.CaloriesKcal, ProteinG = targets?.ProteinG,
                    CarbsG = targets?.CarbsG, FatG = targets?.FatG,
                    CalculatedAt = calculation is null ? (DateTime?)null : context.TimestampUtc,
                    context.TimestampUtc,
                };
                try { planId = await context.QuerySingleAsync<int>(NutritionPlanSql.Insert, parameters); }
                catch (MySqlException ex) when (ex.Number == 1062)
                { throw new ConflictException("A nutrition plan with this name already exists.", code: "plan_name_conflict"); }
                firstCurrent = await context.ExecuteAsync(NutritionPlanSql.SetFirstCurrent,
                    new { CurrentUserId = currentUserId, PlanId = planId }) == 1;
                var rows = new NutritionPlanReadRowData(new(), null, [new() { Id = planId }]);
                return MealPlanMutation<NutritionPlanCreatedRowData>.ChangedResult(new(rows, profileChanged, firstCurrent));
            }, ct, readback: async (context, revision) =>
            {
                var rows = await NutritionPlanReader.ReadAsync(context, currentUserId, planId);
                return new(rows, profileChanged, firstCurrent);
            });
    }
}
