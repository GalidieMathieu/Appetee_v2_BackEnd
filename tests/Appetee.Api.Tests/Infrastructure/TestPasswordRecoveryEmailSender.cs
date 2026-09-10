/*
 * Purpose: Captures password-recovery deliveries so integration tests can exercise real tokens without an external email provider.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

using Appetee.Application.Abstractions.Auth;
using System.Collections.Concurrent;

namespace Appetee.Api.Tests.Infrastructure;

/// <summary>Records test-only recovery messages and can simulate a delivery outage.</summary>
internal sealed class TestPasswordRecoveryEmailSender : IPasswordRecoveryEmailSender
{
    private readonly ConcurrentQueue<RecoveryDelivery> _deliveries = new();

    public bool FailDelivery { get; set; }

    public IReadOnlyList<RecoveryDelivery> Deliveries => _deliveries.ToArray();

    public Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (FailDelivery)
        {
            throw new InvalidOperationException("Simulated recovery delivery failure.");
        }

        _deliveries.Enqueue(new RecoveryDelivery(email, token, expiresAtUtc));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        while (_deliveries.TryDequeue(out _))
        {
        }

        FailDelivery = false;
    }

    /// <summary>Represents one captured recovery email.</summary>
    internal sealed record RecoveryDelivery(
        string Email,
        string Token,
        DateTimeOffset ExpiresAtUtc);
}
