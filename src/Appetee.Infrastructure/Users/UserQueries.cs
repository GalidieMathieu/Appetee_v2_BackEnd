/*
 * Purpose: Reads current-account profile and lifecycle state from MySQL.
 * Change reason: Add E-001 Phase 4 deleted-account session validation.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.RowData;
using Appetee.Infrastructure.Data;
using Dapper;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Models.MealPlans;
using System.Data;
using System.Data.Common;

namespace Appetee.Infrastructure.Users;

public sealed class UserQueries : IUserQueries
{
    private readonly IDbConnectionFactory _db;

    public UserQueries(IDbConnectionFactory db) => _db = db;

    public async Task<NutritionProfileRowData?> GetNutritionProfileAsync(int currentUserId, CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Nutrition profile reads require the configured database connection.");
        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<NutritionProfileRowData>(
                new CommandDefinition(UserSql.GetNutritionProfile, new { CurrentUserId = currentUserId },
                    transaction, commandTimeout: MealPlanRules.SqlCommandTimeoutSeconds, cancellationToken: ct));
            await transaction.CommitAsync(ct);
            return row;
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch { /* Connection disposal cleans up without masking the original failure. */ }
            throw;
        }
    }

    public async Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<CurrentUserProfileRow>(
            new CommandDefinition(
                UserSql.GetCurrentProfile,
                new { currentUserId },
                cancellationToken: ct));

        return row is null
            ? null
            : new CurrentUserProfileDto(row.Username, row.ImageUrl);
    }

    public async Task<bool> CurrentAccountExistsAsync(
        int currentUserId,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleAsync<bool>(
            new CommandDefinition(
                UserSql.CurrentAccountExists,
                new { CurrentUserId = currentUserId },
                cancellationToken: ct));
    }
}
