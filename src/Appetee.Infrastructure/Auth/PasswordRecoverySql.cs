/*
 * Purpose: Centralizes parameterized SQL used by the password-recovery repository.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

namespace Appetee.Infrastructure.Auth;

/// <summary>Provides bounded account lookup, issuance, cleanup, and atomic-consumption statements.</summary>
internal static class PasswordRecoverySql
{
    public const string FindAccount = """
        SELECT id AS UserId, email AS Email
        FROM users
        WHERE email = @Email
        LIMIT 1;
        """;

    public const string LockUser = """
        SELECT id
        FROM users
        WHERE id = @UserId
        FOR UPDATE;
        """;

    public const string LatestTokenCreatedAt = """
        SELECT created_at
        FROM password_reset_tokens
        WHERE user_id = @UserId
        ORDER BY created_at DESC
        LIMIT 1;
        """;

    public const string InsertToken = """
        INSERT INTO password_reset_tokens
            (user_id, token_hash, expires_at, created_at)
        VALUES
            (@UserId, @TokenHash, @ExpiresAtUtc, @CreatedAtUtc);
        """;

    public const string DeleteToken = """
        DELETE FROM password_reset_tokens
        WHERE token_hash = @TokenHash;
        """;

    public const string LockToken = """
        SELECT
            id AS Id,
            user_id AS UserId,
            expires_at AS ExpiresAt,
            used_at AS UsedAt
        FROM password_reset_tokens
        WHERE token_hash = @TokenHash
        LIMIT 1
        FOR UPDATE;
        """;

    public const string ReplacePassword = """
        UPDATE users
        SET password_hash = @PasswordHash,
            updated_at = @NowUtc
        WHERE id = @UserId;
        """;

    public const string ConsumeTokens = """
        UPDATE password_reset_tokens
        SET used_at = @NowUtc
        WHERE user_id = @UserId
          AND used_at IS NULL;
        """;
}
