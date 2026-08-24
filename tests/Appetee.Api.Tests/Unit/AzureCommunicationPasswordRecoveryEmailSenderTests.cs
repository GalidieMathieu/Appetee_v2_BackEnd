/*
 * Purpose: Verifies ACS recovery message construction, asynchronous delivery, and sensitive-data-safe logging without sending real email.
 * Created: 2026-08-23T03:08:53-06:00
 * Last updated: 2026-08-23T03:08:53-06:00
 */

using Appetee.Infrastructure.Auth;
using Azure;
using Microsoft.Extensions.Logging;

namespace Appetee.Api.Tests.Unit;

public sealed class AzureCommunicationPasswordRecoveryEmailSenderTests
{
    [Fact]
    public void BuildRecoveryUrl_TargetsAuthRouteAndEscapesBearerToken()
    {
        var url = AzureCommunicationPasswordRecoveryEmailSender.BuildRecoveryUrl(
            new Uri("https://appetee.test/auth/reset-password"),
            "token +/?");

        Assert.Equal(
            "https://appetee.test/auth/reset-password?token=token%20%2B%2F%3F",
            url.AbsoluteUri);
    }

    [Fact]
    public async Task SendAsync_UsesConfiguredResetPageAndAcsClient()
    {
        var client = new StubCommunicationEmailClient();
        var logger = new CapturingLogger<AzureCommunicationPasswordRecoveryEmailSender>();
        var sender = CreateSender(client, logger);
        using var cancellation = new CancellationTokenSource();

        await sender.SendAsync(
            "member@appetee.test",
            "token +/?",
            new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero),
            cancellation.Token);

        Assert.Equal("DoNotReply@test.azurecomm.net", client.FromAddress);
        Assert.Equal("member@appetee.test", client.RecipientAddress);
        Assert.Equal("Reset your Appetee password", client.Subject);
        Assert.Contains(
            "https://appetee.test/auth/reset-password?token=token%20%2B%2F%3F",
            client.PlainTextContent);
        Assert.Contains("Reset your password", client.HtmlContent);
        Assert.Equal(cancellation.Token, client.CancellationToken);
        Assert.Contains(logger.Messages, message => message.Contains("operation-123"));
    }

    [Fact]
    public async Task SendAsync_ProviderFailureLogsNoTokenOrAccessKey()
    {
        const string token = "raw-reset-token-must-stay-secret";
        const string accessKey = "acs-access-key-must-stay-secret";
        var client = new StubCommunicationEmailClient
        {
            ProviderAccessKey = accessKey,
            Failure = new RequestFailedException(
                401,
                "Provider rejected the request.",
                "Unauthorized",
                null),
        };
        var logger = new CapturingLogger<AzureCommunicationPasswordRecoveryEmailSender>();
        var sender = CreateSender(client, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(
                "member@appetee.test",
                token,
                DateTimeOffset.UtcNow.AddHours(1),
                default));

        var logText = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain(token, logText);
        Assert.DoesNotContain(accessKey, logText);
        Assert.Contains("Status: 401", logText);
        Assert.Contains("Unauthorized", logText);
    }

    private static AzureCommunicationPasswordRecoveryEmailSender CreateSender(
        ICommunicationEmailClient client,
        ILogger<AzureCommunicationPasswordRecoveryEmailSender> logger) =>
        new(
            new CommunicationEmailDeliverySettings(
                new Uri("https://appetee.test/auth/reset-password"),
                "DoNotReply@test.azurecomm.net"),
            client,
            logger);

    /// <summary>Captures ACS request values and simulates provider outcomes.</summary>
    private sealed class StubCommunicationEmailClient : ICommunicationEmailClient
    {
        public string? ProviderAccessKey { get; init; }

        public RequestFailedException? Failure { get; init; }

        public string? FromAddress { get; private set; }

        public string? RecipientAddress { get; private set; }

        public string? Subject { get; private set; }

        public string? PlainTextContent { get; private set; }

        public string? HtmlContent { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<string?> SendAsync(
            string fromAddress,
            string recipientAddress,
            string subject,
            string plainTextContent,
            string htmlContent,
            CancellationToken ct)
        {
            FromAddress = fromAddress;
            RecipientAddress = recipientAddress;
            Subject = subject;
            PlainTextContent = plainTextContent;
            HtmlContent = htmlContent;
            CancellationToken = ct;

            return Failure is null
                ? Task.FromResult<string?>("operation-123")
                : Task.FromException<string?>(Failure);
        }
    }

    /// <summary>Formats structured log entries for assertions without an external provider.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
