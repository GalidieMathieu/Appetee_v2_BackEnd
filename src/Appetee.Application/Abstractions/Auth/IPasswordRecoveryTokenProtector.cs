/*
 * Purpose: Defines secure recovery-token generation and one-way token hashing.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

namespace Appetee.Application.Abstractions.Auth;

/// <summary>Creates bearer secrets and derives the hashes persisted for lookup.</summary>
public interface IPasswordRecoveryTokenProtector
{
    string GenerateToken();

    string HashToken(string token);
}
