/*
 * Purpose: Defines claim-scoped current-account operations.
 * Change reason: Add E-001 Phase 4 account closure.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Dtos;
using Appetee.Application.Requests;

namespace Appetee.Application.Services.Users;

public interface IUserService
{
    Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct);

    Task<CurrentUserProfileDto?> UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct);

    Task<bool> CloseCurrentAccountAsync(
        int currentUserId,
        CancellationToken ct);

}
