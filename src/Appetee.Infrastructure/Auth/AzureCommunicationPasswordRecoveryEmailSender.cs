/*
 * Purpose: Delivers password-recovery links through Azure Communication Services Email without exposing bearer tokens or provider credentials.
 * Created: 2026-08-23T03:08:53-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;

namespace Appetee.Infrastructure.Auth;

/// <summary>Contains the non-secret reset destination and verified ACS sender address used for recovery delivery.</summary>
public sealed class CommunicationEmailDeliverySettings
{
    public CommunicationEmailDeliverySettings(Uri resetPageUrl, string fromAddress)
    {
        ResetPageUrl = resetPageUrl;
        FromAddress = fromAddress;
    }

    public Uri ResetPageUrl { get; }

    public string FromAddress { get; }
}

/// <summary>Provides the narrow ACS client operation needed by password-recovery delivery.</summary>
public interface ICommunicationEmailClient
{
    Task<string?> SendAsync(
        string fromAddress,
        string recipientAddress,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken ct);
}

/// <summary>Adapts the official Azure Communication Services Email SDK to the recovery delivery boundary.</summary>
public sealed class AzureCommunicationEmailClient(EmailClient client)
    : ICommunicationEmailClient
{
    public async Task<string?> SendAsync(
        string fromAddress,
        string recipientAddress,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken ct)
    {
        var content = new EmailContent(subject)
        {
            PlainText = plainTextContent,
            Html = htmlContent,
        };
        var recipients = new EmailRecipients(
            [new EmailAddress(recipientAddress)]);
        var message = new EmailMessage(fromAddress, recipients, content);

        var operation = await client.SendAsync(
            WaitUntil.Completed,
            message,
            ct);

        return operation.Id;
    }
}

/// <summary>Builds the one-time recovery message and sends it through Azure Communication Services Email.</summary>
public sealed class AzureCommunicationPasswordRecoveryEmailSender(
    CommunicationEmailDeliverySettings settings,
    ICommunicationEmailClient client,
    ILogger<AzureCommunicationPasswordRecoveryEmailSender> logger)
    : IPasswordRecoveryEmailSender
{
    private const string Subject = "Reset your Appetee password";

    public async Task SendAsync(
        string email,
        string token,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct)
    {
        var recoveryUrl = BuildRecoveryUrl(settings.ResetPageUrl, token);
        var plainText =
            "Use this single-use link to reset your Appetee password: " +
            $"{recoveryUrl.AbsoluteUri}{Environment.NewLine}{Environment.NewLine}" +
            $"This link expires at {expiresAtUtc:O}.";
        var encodedUrl = HtmlEncoder.Default.Encode(recoveryUrl.AbsoluteUri);
        var html =
            "<p>Use this single-use link to reset your Appetee password:</p>" +
            $"<p><a href=\"{encodedUrl}\">Reset your password</a></p>" +
            $"<p>This link expires at {expiresAtUtc:O}.</p>";

        try
        {
            var operationId = await client.SendAsync(
                settings.FromAddress,
                email,
                Subject,
                plainText,
                html,
                ct);

            logger.LogInformation(
                "Password recovery email was sent through Azure Communication Services. " +
                "Operation ID: {OperationId}.",
                operationId ?? "unavailable");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (RequestFailedException exception)
        {
            logger.LogWarning(
                "Azure Communication Services rejected a password recovery email. " +
                "Status: {Status}; error code: {ErrorCode}.",
                exception.Status,
                exception.ErrorCode ?? "unavailable");
            throw new InvalidOperationException(
                "Password recovery email delivery failed.",
                exception);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Azure Communication Services password recovery email delivery failed before completion.");
            throw new InvalidOperationException(
                "Password recovery email delivery failed.",
                exception);
        }
    }

    internal static Uri BuildRecoveryUrl(Uri resetPageUrl, string token) =>
        new($"{resetPageUrl.AbsoluteUri}?token={Uri.EscapeDataString(token)}");
}
