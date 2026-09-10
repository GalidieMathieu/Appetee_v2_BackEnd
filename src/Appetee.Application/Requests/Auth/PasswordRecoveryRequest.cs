/*
 * Purpose: Defines the anonymous request used to start password recovery.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

namespace Appetee.Application.Requests.Auth;

/// <summary>Supplies the account email for a privacy-preserving recovery request.</summary>
public sealed record PasswordRecoveryRequest(string Email);
