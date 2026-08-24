/*
 * Purpose: Defines delivery of a password-recovery link without coupling application logic to an email provider.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

namespace Appetee.Application.Abstractions.Auth;

/// <summary>Delivers the raw recovery proof only to the selected account address.</summary>
public interface IPasswordRecoveryEmailSender
{
    Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct);
}
