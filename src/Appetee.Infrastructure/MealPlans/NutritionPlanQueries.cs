using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Infrastructure.Data;
using System.Data;
using System.Data.Common;

namespace Appetee.Infrastructure.MealPlans;

public sealed class NutritionPlanQueries(IDbConnectionFactory db) : INutritionPlanQueries
{
    public Task<NutritionPlanReadRowData> ListAsync(int currentUserId, CancellationToken ct) => ReadAsync(currentUserId, null, ct);
    public Task<NutritionPlanReadRowData> GetAsync(int currentUserId, int planId, CancellationToken ct) => ReadAsync(currentUserId, planId, ct);

    private async Task<NutritionPlanReadRowData> ReadAsync(int currentUserId, int? planId, CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        if (connection is not DbConnection dbConnection) throw new InvalidOperationException("Nutrition plan reads require the configured connection.");
        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            var result = await NutritionPlanReader.ReadAsync(connection, transaction, currentUserId, planId, ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { /* Preserve original failure. */ }
            throw;
        }
    }
}
