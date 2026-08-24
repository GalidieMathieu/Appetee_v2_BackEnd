/*
 * Purpose: Verifies password-recovery privacy, delivery cleanup, and replacement-password policy.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Application.utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Appetee.Api.Tests.Unit;

public sealed class PasswordRecoveryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 23, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RequestAsync_ReturnsSameOutcomeForKnownAndUnknownAccounts()
    {
        var knownRepository = new StubRepository
        {
            Account = new PasswordRecoveryAccount(12, "known@appetee.test"),
        };
        var unknownRepository = new StubRepository();
        var knownSender = new StubEmailSender();
        var unknownSender = new StubEmailSender();

        var knownResult = await CreateService(knownRepository, knownSender).RequestAsync(
            new PasswordRecoveryRequest("known@appetee.test"),
            default);
        var unknownResult = await CreateService(unknownRepository, unknownSender).RequestAsync(
            new PasswordRecoveryRequest("unknown@appetee.test"),
            default);

        Assert.Equal(knownResult, unknownResult);
        Assert.Equal(PasswordRecoveryService.GenericRequestMessage, knownResult.Message);
        Assert.Equal(1, knownSender.CallCount);
        Assert.Equal("known@appetee.test", knownSender.Email);
        Assert.Equal("raw-token", knownSender.Token);
        Assert.Equal(0, unknownSender.CallCount);
    }

    [Fact]
    public async Task RequestAsync_RemovesTokenWhenDeliveryFails()
    {
        var repository = new StubRepository
        {
            Account = new PasswordRecoveryAccount(12, "known@appetee.test"),
        };
        var sender = new StubEmailSender { Fail = true };

        var result = await CreateService(repository, sender).RequestAsync(
            new PasswordRecoveryRequest("known@appetee.test"),
            default);

        Assert.Equal(PasswordRecoveryService.GenericRequestMessage, result.Message);
        Assert.Equal("hashed-token", repository.DeletedTokenHash);
    }

    [Theory]
    [InlineData("", "New password is required.")]
    [InlineData("short", "between 8 and 128")]
    public async Task ConfirmAsync_RejectsPasswordBelowConfiguredMinimum(
        string password,
        string expectedMessage)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService(new StubRepository()).ConfirmAsync(
                new PasswordRecoveryConfirmRequest("valid-token", password),
                default));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task ConfirmAsync_UsesStableErrorForRejectedToken()
    {
        var exception = await Assert.ThrowsAsync<InvalidPasswordRecoveryTokenException>(() =>
            CreateService(new StubRepository()).ConfirmAsync(
                new PasswordRecoveryConfirmRequest("invalid-token", "NewPassword123!"),
                default));

        Assert.Equal("password_recovery_token_invalid", exception.Code);
        Assert.Equal("The password recovery link is invalid or expired.", exception.Message);
    }

    private static PasswordRecoveryService CreateService(
        StubRepository repository,
        StubEmailSender? sender = null) =>
        new(
            repository,
            new StubTokenProtector(),
            sender ?? new StubEmailSender(),
            new StubPasswordHasher(),
            new PasswordRecoverySettings(
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(1)),
            new FixedTimeProvider(Now),
            NullLogger<PasswordRecoveryService>.Instance);

    /// <summary>Captures recovery persistence calls for service-level assertions.</summary>
    private sealed class StubRepository : IPasswordRecoveryRepository
    {
        public PasswordRecoveryAccount? Account { get; init; }

        public string? DeletedTokenHash { get; private set; }

        public Task<PasswordRecoveryAccount?> FindAccountAsync(
            string email,
            CancellationToken ct) =>
            Task.FromResult(Account);

        public Task<bool> TryIssueTokenAsync(
            int userId,
            string tokenHash,
            DateTime createdAtUtc,
            DateTime expiresAtUtc,
            TimeSpan cooldown,
            CancellationToken ct) =>
            Task.FromResult(true);

        public Task DeleteTokenAsync(
            string tokenHash,
            CancellationToken ct)
        {
            DeletedTokenHash = tokenHash;
            return Task.CompletedTask;
        }

        public Task<bool> TryReplacePasswordAsync(
            string tokenHash,
            string passwordHash,
            DateTime nowUtc,
            CancellationToken ct) =>
            Task.FromResult(false);
    }

    /// <summary>Returns deterministic secrets and hashes for service tests.</summary>
    private sealed class StubTokenProtector : IPasswordRecoveryTokenProtector
    {
        public string GenerateToken() => "raw-token";

        public string HashToken(string token) => "hashed-token";
    }

    /// <summary>Completes delivery or raises a controlled failure.</summary>
    private sealed class StubEmailSender : IPasswordRecoveryEmailSender
    {
        public bool Fail { get; init; }

        public int CallCount { get; private set; }

        public string? Email { get; private set; }

        public string? Token { get; private set; }

        public Task SendAsync(
            string email,
            string token,
            DateTimeOffset expiresAtUtc,
            CancellationToken ct)
        {
            CallCount++;
            Email = email;
            Token = token;

            return Fail
                ? Task.FromException(new InvalidOperationException("Delivery failed."))
                : Task.CompletedTask;
        }
    }

    /// <summary>Produces a recognizable password hash without invoking production hashing cost.</summary>
    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string passwordHash) => false;
    }

    /// <summary>Freezes time at the recovery test boundary.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
