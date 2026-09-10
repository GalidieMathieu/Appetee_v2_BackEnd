/*
 * Purpose: Represents credential-check outcomes so the service can map failures without issuing a session cookie.
 * Created: 2026-08-21T10:34:58-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

using Appetee.Application.Models.Auth;

namespace Appetee.Application.Requests.Auth;

/// Identifies the safe product outcome of a credential check.
public enum LoginOutcome
{
    Authenticated,
    InvalidCredentials,
    EmailVerificationRequired
}

/// Carries authenticated user data only when the credential check succeeds.
public sealed record LoginAttempt(
    LoginOutcome Outcome,
    AuthResult? AuthResult = null)
{
    public static LoginAttempt Authenticated(AuthResult authResult) =>
        new(LoginOutcome.Authenticated, authResult);

    public static LoginAttempt InvalidCredentials() =>
        new(LoginOutcome.InvalidCredentials);

    public static LoginAttempt EmailVerificationRequired() =>
        new(LoginOutcome.EmailVerificationRequired);
}
