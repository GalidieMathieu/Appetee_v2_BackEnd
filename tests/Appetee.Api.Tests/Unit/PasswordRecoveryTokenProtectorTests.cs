/*
 * Purpose: Verifies recovery bearer tokens have sufficient entropy and are never stored in raw form.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

using Appetee.Infrastructure.Auth;

namespace Appetee.Api.Tests.Unit;

public sealed class PasswordRecoveryTokenProtectorTests
{
    private readonly PasswordRecoveryTokenProtector _protector = new();

    [Fact]
    public void GenerateToken_ReturnsDistinctBase64UrlSecrets()
    {
        var first = _protector.GenerateToken();
        var second = _protector.GenerateToken();

        Assert.Equal(43, first.Length);
        Assert.DoesNotContain("=", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashToken_IsStableAndDoesNotContainRawToken()
    {
        const string token = "test-recovery-token";

        var hash = _protector.HashToken(token);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, _protector.HashToken(token));
        Assert.DoesNotContain(token, hash, StringComparison.OrdinalIgnoreCase);
    }
}
