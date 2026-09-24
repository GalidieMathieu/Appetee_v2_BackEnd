/*
 * Purpose: Deletes the current account transactionally and persists retryable cleanup work.
 * Change reason: Implement E-001 Phase 4 relational account closure.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Users;
using Appetee.Application.Models.Users;
using Appetee.Infrastructure.Data;
using Dapper;
using System.Data;

namespace Appetee.Infrastructure.Users;

/// <summary>Uses one MySQL transaction so cleanup work cannot be lost when an account is deleted.</summary>
public sealed class AccountClosureRepository : IAccountClosureRepository
{
    private readonly IDbConnectionFactory _db;

    public AccountClosureRepository(IDbConnectionFactory db) => _db = db;

    // Locking the claim-scoped row serializes closure and captures media before cascading deletion.
    public async Task<long?> CloseCurrentAccountAsync(
        int currentUserId,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            var account = await connection.QuerySingleOrDefaultAsync<AccountRow>(
                new CommandDefinition(
                    AccountClosureSql.LockCurrentAccount,
                    new { CurrentUserId = currentUserId },
                    transaction,
                    cancellationToken: ct));

            if (account is null)
            {
                transaction.Rollback();
                return null;
            }

            var cleanupId = await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    AccountClosureSql.EnqueueCleanup,
                    new
                    {
                        CurrentUserId = currentUserId,
                        account.ProfileImageUrl,
                    },
                    transaction,
                    cancellationToken: ct));

            var deleted = await connection.ExecuteAsync(
                new CommandDefinition(
                    AccountClosureSql.DeleteCurrentAccount,
                    new { CurrentUserId = currentUserId },
                    transaction,
                    cancellationToken: ct));

            if (deleted != 1)
            {
                throw new InvalidOperationException(
                    "The locked current account was not deleted.");
            }

            transaction.Commit();
            return cleanupId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<AccountClosureCleanup?> GetPendingCleanupAsync(
        long cleanupId,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AccountClosureCleanup>(
            new CommandDefinition(
                AccountClosureSql.GetPendingCleanup,
                new { CleanupId = cleanupId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<long>> ListDueCleanupIdsAsync(
        int maximumCount,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        var ids = await connection.QueryAsync<long>(
            new CommandDefinition(
                AccountClosureSql.ListDueCleanupIds,
                new { MaximumCount = maximumCount },
                cancellationToken: ct));
        return ids.AsList();
    }

    public async Task MarkCleanupCompletedAsync(
        long cleanupId,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                AccountClosureSql.MarkCleanupCompleted,
                new { CleanupId = cleanupId },
                cancellationToken: ct));
    }

    public async Task MarkCleanupFailedAsync(
        long cleanupId,
        string errorCode,
        DateTimeOffset nextAttemptUtc,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                AccountClosureSql.MarkCleanupFailed,
                new
                {
                    CleanupId = cleanupId,
                    ErrorCode = errorCode,
                    NextAttemptUtc = nextAttemptUtc.UtcDateTime,
                },
                cancellationToken: ct));
    }

    private sealed record AccountRow(string? ProfileImageUrl);
}
