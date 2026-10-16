/*
 * Purpose: Defines transactional account deletion and its durable cleanup outbox.
 * Change reason: Add E-001 Phase 4 account-closure persistence boundaries.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Models.Users;

namespace Appetee.Application.Abstractions.Users;

/// <summary>Persists claim-scoped account closure and retryable post-commit cleanup state.</summary>
public interface IAccountClosureRepository
{
    Task<long?> CloseCurrentAccountAsync(
        int currentUserId,
        CancellationToken ct);

    Task<AccountClosureCleanup?> GetPendingCleanupAsync(
        long cleanupId,
        CancellationToken ct);

    Task<IReadOnlyList<long>> ListDueCleanupIdsAsync(
        int maximumCount,
        CancellationToken ct);

    Task MarkCleanupCompletedAsync(
        long cleanupId,
        CancellationToken ct);

    Task MarkCleanupFailedAsync(
        long cleanupId,
        string errorCode,
        DateTimeOffset nextAttemptUtc,
        CancellationToken ct);
}
