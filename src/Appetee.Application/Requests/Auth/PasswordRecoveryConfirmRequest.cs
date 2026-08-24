/*
 * Purpose: Defines the proof and replacement password used to finish password recovery.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

namespace Appetee.Application.Requests.Auth;

/// <summary>Supplies a recovery token and the new password it authorizes.</summary>
public sealed record PasswordRecoveryConfirmRequest(
    string Token,
    string NewPassword);
