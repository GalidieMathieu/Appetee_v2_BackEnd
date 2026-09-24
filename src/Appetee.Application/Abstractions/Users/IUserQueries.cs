/*
 * Purpose: Defines current-account read operations used by profile and session boundaries.
 * Change reason: Add E-001 Phase 4 deleted-account session invalidation.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Dtos;

namespace Appetee.Application.Abstractions.Users;

public interface IUserQueries
{
    Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct);

    Task<bool> CurrentAccountExistsAsync(
        int currentUserId,
        CancellationToken ct);
}
