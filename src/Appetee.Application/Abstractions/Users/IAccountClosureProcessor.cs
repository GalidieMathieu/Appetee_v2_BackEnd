/*
 * Purpose: Defines idempotent processing and reconciliation of account-closure cleanup jobs.
 * Change reason: Add E-001 Phase 4 retryable media cleanup.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

namespace Appetee.Application.Abstractions.Users;

/// <summary>Processes one cleanup job or a bounded batch without exposing deleted account identity.</summary>
public interface IAccountClosureProcessor
{
    Task<bool> TryProcessAsync(long cleanupId, CancellationToken ct);

    Task ProcessDueAsync(int maximumCount, CancellationToken ct);
}
