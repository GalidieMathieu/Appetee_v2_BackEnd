/*
 * Purpose: Coordinates generic recovery requests, delivery cleanup, and single-use password replacement.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T01:28:02-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Dtos;
using Appetee.Application.Requests.Auth;
using Appetee.Application.utils;
using Microsoft.Extensions.Logging;
using System.Net.Mail;

namespace Appetee.Application.Services.Auth;

/// <summary>Runs the recovery lifecycle while keeping account existence out of public outcomes.</summary>
public sealed class PasswordRecoveryService : IPasswordRecoveryService
{
    public const string GenericRequestMessage =
        "If an eligible account matches that email, recovery instructions will be sent.";

    private readonly IPasswordRecoveryRepository _repository;
    private readonly IPasswordRecoveryTokenProtector _tokenProtector;
    private readonly IPasswordRecoveryEmailSender _emailSender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly PasswordRecoverySettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PasswordRecoveryService> _logger;

    public PasswordRecoveryService(
        IPasswordRecoveryRepository repository,
        IPasswordRecoveryTokenProtector tokenProtector,
        IPasswordRecoveryEmailSender emailSender,
        IPasswordHasher passwordHasher,
        PasswordRecoverySettings settings,
        TimeProvider timeProvider,
        ILogger<PasswordRecoveryService> logger)
    {
        _repository = repository;
        _tokenProtector = tokenProtector;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PasswordRecoveryRequestDto> RequestAsync(
        PasswordRecoveryRequest request,
        CancellationToken ct)
    {
        var email = ValidateEmail(request.Email);
        var token = _tokenProtector.GenerateToken();
        var tokenHash = _tokenProtector.HashToken(token);
        var account = await _repository.FindAccountAsync(email, ct);

        if (account is null)
        {
            return GenericResult();
        }

        var issuedAt = _timeProvider.GetUtcNow();
        var issued = await _repository.TryIssueTokenAsync(
            account.UserId,
            tokenHash,
            issuedAt.UtcDateTime,
            issuedAt.Add(_settings.TokenLifetime).UtcDateTime,
            _settings.RequestCooldown,
            ct);

        if (!issued)
        {
            return GenericResult();
        }

        try
        {
            await _emailSender.SendAsync(
                account.Email,
                token,
                issuedAt.Add(_settings.TokenLifetime),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RemoveUndeliveredTokenAsync(tokenHash, account.UserId);
            throw;
        }
        catch
        {
            var tokenRemoved = await RemoveUndeliveredTokenAsync(
                tokenHash,
                account.UserId);
            _logger.LogWarning(
                "Password recovery delivery failed for account {UserId}; " +
                "token cleanup succeeded: {TokenRemoved}.",
                account.UserId,
                tokenRemoved);
        }

        return GenericResult();
    }

    public async Task ConfirmAsync(
        PasswordRecoveryConfirmRequest request,
        CancellationToken ct)
    {
        ValidateReplacementPassword(request.NewPassword);

        if (string.IsNullOrWhiteSpace(request.Token)
            || request.Token.Length > 512)
        {
            throw new InvalidPasswordRecoveryTokenException();
        }

        var replaced = await _repository.TryReplacePasswordAsync(
            _tokenProtector.HashToken(request.Token),
            _passwordHasher.Hash(request.NewPassword),
            _timeProvider.GetUtcNow().UtcDateTime,
            ct);

        if (!replaced)
        {
            throw new InvalidPasswordRecoveryTokenException();
        }
    }

    private static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Email is required.");
        }

        var normalized = email.Trim();
        if (normalized.Length > 255 || !MailAddress.TryCreate(normalized, out _))
        {
            throw new ValidationException("Email must be valid.");
        }

        return normalized;
    }

    private void ValidateReplacementPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ValidationException("New password is required.");
        }

        if (password.Length < PasswordRecoverySettings.MinimumPasswordLength
            || password.Length > PasswordRecoverySettings.MaximumPasswordLength)
        {
            throw new ValidationException(
                $"New password must be between {PasswordRecoverySettings.MinimumPasswordLength} " +
                $"and {PasswordRecoverySettings.MaximumPasswordLength} characters.");
        }
    }

    private static PasswordRecoveryRequestDto GenericResult() =>
        new(GenericRequestMessage);

    private async Task<bool> RemoveUndeliveredTokenAsync(
        string tokenHash,
        int userId)
    {
        try
        {
            // A failed delivery must not leave behind a valid secret the user never received.
            await _repository.DeleteTokenAsync(tokenHash, CancellationToken.None);
            return true;
        }
        catch
        {
            _logger.LogError(
                "Password recovery token cleanup failed for account {UserId}.",
                userId);
            return false;
        }
    }
}
