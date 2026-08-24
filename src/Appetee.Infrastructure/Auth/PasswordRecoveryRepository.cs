/*
 * Purpose: Persists hashed recovery tokens and atomically consumes them while replacing a password.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:36:40-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Models.Auth;
using Appetee.Infrastructure.Data;
using Dapper;
using System.Data.Common;

namespace Appetee.Infrastructure.Auth;

/// <summary>Implements recovery-token throttling and single-use consumption with MySQL transactions.</summary>
public sealed class PasswordRecoveryRepository(
    IDbConnectionFactory db) : IPasswordRecoveryRepository
{
    public async Task<PasswordRecoveryAccount?> FindAccountAsync(
        string email,
        CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PasswordRecoveryAccount>(
            new CommandDefinition(
                PasswordRecoverySql.FindAccount,
                new { Email = email },
                cancellationToken: ct));
    }

    public async Task<bool> TryIssueTokenAsync(
        int userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        TimeSpan cooldown,
        CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        var dbConnection = (DbConnection)connection;
        await using var transaction = await dbConnection.BeginTransactionAsync(ct);

        try
        {
            var lockedUserId = await dbConnection.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(
                    PasswordRecoverySql.LockUser,
                    new { UserId = userId },
                    transaction,
                    cancellationToken: ct));

            if (lockedUserId is null)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            var latestCreatedAt =
                await dbConnection.QuerySingleOrDefaultAsync<DateTime?>(
                    new CommandDefinition(
                        PasswordRecoverySql.LatestTokenCreatedAt,
                        new { UserId = userId },
                        transaction,
                        cancellationToken: ct));

            if (latestCreatedAt.HasValue
                && latestCreatedAt.Value > createdAtUtc.Subtract(cooldown))
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    PasswordRecoverySql.InsertToken,
                    new
                    {
                        UserId = userId,
                        TokenHash = tokenHash,
                        ExpiresAtUtc = expiresAtUtc,
                        CreatedAtUtc = createdAtUtc,
                    },
                    transaction,
                    cancellationToken: ct));

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteTokenAsync(
        string tokenHash,
        CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                PasswordRecoverySql.DeleteToken,
                new { TokenHash = tokenHash },
                cancellationToken: ct));
    }

    public async Task<bool> TryReplacePasswordAsync(
        string tokenHash,
        string passwordHash,
        DateTime nowUtc,
        CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        var dbConnection = (DbConnection)connection;
        await using var transaction = await dbConnection.BeginTransactionAsync(ct);

        try
        {
            var token = await dbConnection.QuerySingleOrDefaultAsync<RecoveryTokenRow>(
                new CommandDefinition(
                    PasswordRecoverySql.LockToken,
                    new { TokenHash = tokenHash },
                    transaction,
                    cancellationToken: ct));

            if (token is null
                || token.UsedAt.HasValue
                || token.ExpiresAt <= nowUtc)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    PasswordRecoverySql.ReplacePassword,
                    new
                    {
                        UserId = token.UserId,
                        PasswordHash = passwordHash,
                        NowUtc = nowUtc,
                    },
                    transaction,
                    cancellationToken: ct));

            // Consuming one proof invalidates every outstanding recovery link for the account.
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    PasswordRecoverySql.ConsumeTokens,
                    new
                    {
                        UserId = token.UserId,
                        NowUtc = nowUtc,
                    },
                    transaction,
                    cancellationToken: ct));

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Maps the locked token state needed to decide whether consumption may proceed.</summary>
    private sealed record RecoveryTokenRow(
        long Id,
        int UserId,
        DateTime ExpiresAt,
        DateTime? UsedAt);
}
