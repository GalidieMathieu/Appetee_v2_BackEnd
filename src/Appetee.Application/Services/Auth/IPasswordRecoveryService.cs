/*
 * Purpose: Defines the application operations exposed by the password-recovery API.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

using Appetee.Application.Dtos;
using Appetee.Application.Requests.Auth;

namespace Appetee.Application.Services.Auth;

/// <summary>Coordinates privacy-safe recovery requests and single-use password replacement.</summary>
public interface IPasswordRecoveryService
{
    Task<PasswordRecoveryRequestDto> RequestAsync(
        PasswordRecoveryRequest request,
        CancellationToken ct);

    Task ConfirmAsync(
        PasswordRecoveryConfirmRequest request,
        CancellationToken ct);
}
