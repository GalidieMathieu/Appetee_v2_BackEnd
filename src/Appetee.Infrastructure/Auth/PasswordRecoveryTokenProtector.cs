/*
 * Purpose: Generates high-entropy password-recovery bearer tokens and hashes them before persistence.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace Appetee.Infrastructure.Auth;

/// <summary>Protects recovery tokens using 256 bits of randomness and SHA-256 storage hashes.</summary>
public sealed class PasswordRecoveryTokenProtector : IPasswordRecoveryTokenProtector
{
    public string GenerateToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
