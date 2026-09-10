/*
 * Purpose: Returns one stable outcome for password-recovery requests without disclosing account existence.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

namespace Appetee.Application.Dtos;

/// <summary>Provides the generic next-step message shared by every eligible request outcome.</summary>
public sealed record PasswordRecoveryRequestDto(string Message);
