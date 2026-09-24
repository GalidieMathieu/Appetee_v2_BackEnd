/*
 * Purpose: Carries the durable, non-account-identifying work needed after relational account deletion.
 * Change reason: Add E-001 Phase 4 account-closure cleanup coordination.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

namespace Appetee.Application.Models.Users;

/// <summary>Identifies a committed account closure and its optional profile-media cleanup.</summary>
public sealed record AccountClosureCleanup(
    long Id,
    int FormerUserId,
    string? ProfileImageUrl,
    uint AttemptCount);
