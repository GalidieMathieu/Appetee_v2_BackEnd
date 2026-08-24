/*
 * Purpose: Carries the minimum account data needed to issue a password-recovery message.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

namespace Appetee.Application.Models.Auth;

/// <summary>Identifies a recovery-eligible account and its destination address.</summary>
public sealed record PasswordRecoveryAccount(int UserId, string Email);
