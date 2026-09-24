/*
 * Purpose: Centralizes transactional account-closure and cleanup-outbox SQL.
 * Change reason: Add E-001 Phase 4 durable account deletion and reconciliation.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

namespace Appetee.Infrastructure.Users;

internal static class AccountClosureSql
{
    internal const string LockCurrentAccount = """
        SELECT image_url AS ProfileImageUrl
        FROM users
        WHERE id = @CurrentUserId
        LIMIT 1
        FOR UPDATE;
    """;

    internal const string EnqueueCleanup = """
        INSERT INTO account_closure_cleanup (
            former_user_id,
            profile_image_url,
            status,
            attempt_count,
            next_attempt_at,
            created_at,
            updated_at
        )
        VALUES (
            @CurrentUserId,
            @ProfileImageUrl,
            'pending',
            0,
            UTC_TIMESTAMP(),
            UTC_TIMESTAMP(),
            UTC_TIMESTAMP()
        );

        SELECT LAST_INSERT_ID();
    """;

    internal const string DeleteCurrentAccount = """
        DELETE FROM users
        WHERE id = @CurrentUserId;
    """;

    internal const string GetPendingCleanup = """
        SELECT
            id                AS Id,
            former_user_id    AS FormerUserId,
            profile_image_url AS ProfileImageUrl,
            attempt_count     AS AttemptCount
        FROM account_closure_cleanup
        WHERE id = @CleanupId
          AND status = 'pending'
        LIMIT 1;
    """;

    internal const string ListDueCleanupIds = """
        SELECT id
        FROM account_closure_cleanup
        WHERE status = 'pending'
          AND next_attempt_at <= UTC_TIMESTAMP()
        ORDER BY next_attempt_at, id
        LIMIT @MaximumCount;
    """;

    internal const string MarkCleanupCompleted = """
        UPDATE account_closure_cleanup
        SET
            status = 'completed',
            former_user_id = NULL,
            profile_image_url = NULL,
            last_error_code = NULL,
            completed_at = UTC_TIMESTAMP(),
            updated_at = UTC_TIMESTAMP()
        WHERE id = @CleanupId
          AND status = 'pending';
    """;

    internal const string MarkCleanupFailed = """
        UPDATE account_closure_cleanup
        SET
            attempt_count = attempt_count + 1,
            last_error_code = @ErrorCode,
            next_attempt_at = @NextAttemptUtc,
            updated_at = UTC_TIMESTAMP()
        WHERE id = @CleanupId
          AND status = 'pending';
    """;
}
