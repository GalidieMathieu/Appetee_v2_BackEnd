using Appetee.Application.Abstractions.Users;
using Appetee.Application.Requests;
using Appetee.Infrastructure.Data;
using Dapper;
using Appetee.Infrastructure.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Models.MealPlans;

namespace Appetee.Infrastructure.Users;

internal sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly MealPlanTransaction _mealPlans;

    public UserRepository(IDbConnectionFactory db, MealPlanTransaction mealPlans)
    {
        _db = db;
        _mealPlans = mealPlans;
    }

    public Task<MealPlanWriteResult<NutritionProfileRowData>> UpdateNutritionProfileAsync(
        int currentUserId, NutritionProfileRequest request, long expectedRevision, CancellationToken ct) =>
        _mealPlans.ExecuteAsync("nutrition-profile.update", currentUserId, expectedRevision,
            async context =>
            {
                var current = await context.QuerySingleAsync<NutritionProfileRowData>(
                    UserSql.GetNutritionProfile, new { CurrentUserId = currentUserId });
                if (current.CalculationSex == request.CalculationSex && current.BirthYear == request.BirthYear
                    && current.HeightCm == request.HeightCm && current.WeightKg == request.WeightKg)
                    return MealPlanMutation<NutritionProfileRowData>.UnchangedResult(current);
                await context.ExecuteAsync(UserSql.UpdateNutritionProfile, new
                {
                    CurrentUserId = currentUserId, request.CalculationSex, request.BirthYear,
                    request.HeightCm, request.WeightKg,
                });
                return MealPlanMutation<NutritionProfileRowData>.ChangedResult(current);
            }, ct,
            readback: (context, revision) => context.QuerySingleAsync<NutritionProfileRowData>(
                UserSql.GetNutritionProfile, new { CurrentUserId = currentUserId }));

    public async Task UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                UserSql.UpdateCurrentProfile,
                new
                {
                    currentUserId,
                    username = request.Username,
                    imageUrl = request.ImageUrl
                },
                cancellationToken: ct));
    }
}
