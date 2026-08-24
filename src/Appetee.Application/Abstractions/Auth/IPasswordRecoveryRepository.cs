/*
 * Purpose: Defines persistence operations for issuing and atomically consuming password-recovery tokens.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

using Appetee.Application.Models.Auth;

namespace Appetee.Application.Abstractions.Auth;

/// <summary>Stores hashed recovery tokens and replaces passwords in a single-use transaction.</summary>
public interface IPasswordRecoveryRepository
{
    Task<PasswordRecoveryAccount?> FindAccountAsync(
        string email,
        CancellationToken ct);

    Task<bool> TryIssueTokenAsync(
        int userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        TimeSpan cooldown,
        CancellationToken ct);

    Task DeleteTokenAsync(string tokenHash, CancellationToken ct);

    Task<bool> TryReplacePasswordAsync(
        string tokenHash,
        string passwordHash,
        DateTime nowUtc,
        CancellationToken ct);
}
