/*
 * Purpose: Verifies the password-recovery API across valid, expired, used, invalid, throttled, and concurrent token paths.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class PasswordRecoveryEndpointsTests(
    AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Request_ReturnsSameGenericOutcome_ForKnownAndUnknownEmail()
    {
        const string knownEmail = "recovery_known@appetee.test";
        await SignUpAsync(knownEmail, "OriginalPassword123!");

        using var knownResponse = await RequestRecoveryAsync(knownEmail);
        var knownResult = await knownResponse.Content
            .ReadFromJsonAsync<PasswordRecoveryRequestDto>();
        Factory.RecoveryEmailSender.Reset();
        using var unknownResponse = await RequestRecoveryAsync(
            "recovery_unknown@appetee.test");
        var unknownResult = await unknownResponse.Content
            .ReadFromJsonAsync<PasswordRecoveryRequestDto>();

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
        Assert.Equal(knownResult, unknownResult);
        Assert.Equal(PasswordRecoveryService.GenericRequestMessage, knownResult!.Message);
        Assert.Empty(Factory.RecoveryEmailSender.Deliveries);
    }

    [Fact]
    public async Task Confirm_ReplacesPassword_AndConsumesTokenOnce()
    {
        const string email = "recovery_valid@appetee.test";
        const string originalPassword = "OriginalPassword123!";
        const string newPassword = "ReplacementPassword123!";
        await SignUpAsync(email, originalPassword);
        var token = await IssueTokenAsync(email);

        using var confirmResponse = await Client.PostAsJsonAsync(
            "/api/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, newPassword));
        using var repeatedResponse = await Client.PostAsJsonAsync(
            "/api/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, "AnotherPassword123!"));

        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);
        await AssertInvalidTokenAsync(repeatedResponse);
        await AssertLoginAsync(email, originalPassword, HttpStatusCode.Unauthorized);
        await AssertLoginAsync(email, newPassword, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Confirm_ReturnsStableInvalidOutcome_ForExpiredToken()
    {
        const string email = "recovery_expired@appetee.test";
        await SignUpAsync(email, "OriginalPassword123!");
        var token = await IssueTokenAsync(email);
        await Factory.Database.ExecuteAsync(
            "UPDATE password_reset_tokens SET expires_at = UTC_TIMESTAMP() - INTERVAL 1 SECOND;");

        using var response = await Client.PostAsJsonAsync(
            "/api/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, "ReplacementPassword123!"));

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task Confirm_ReturnsSameInvalidOutcome_ForUnknownToken()
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest("unknown-token", "ReplacementPassword123!"));

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task Request_ThrottlesRepeatedIssuance_WithoutChangingPublicOutcome()
    {
        const string email = "recovery_throttle@appetee.test";
        await SignUpAsync(email, "OriginalPassword123!");

        using var first = await RequestRecoveryAsync(email);
        using var second = await RequestRecoveryAsync(email);
        var tokenCount = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM password_reset_tokens;");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Single(Factory.RecoveryEmailSender.Deliveries);
        Assert.Equal(1, tokenCount);
    }

    [Fact]
    public async Task Request_RemovesTokenAfterDeliveryFailure_AndRemainsGeneric()
    {
        const string email = "recovery_delivery_failure@appetee.test";
        await SignUpAsync(email, "OriginalPassword123!");
        Factory.RecoveryEmailSender.FailDelivery = true;

        using var response = await RequestRecoveryAsync(email);
        var result = await response.Content.ReadFromJsonAsync<PasswordRecoveryRequestDto>();
        var tokenCount = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM password_reset_tokens;");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(PasswordRecoveryService.GenericRequestMessage, result!.Message);
        Assert.Equal(0, tokenCount);
    }

    [Fact]
    public async Task Confirm_AllowsOnlyOneConcurrentConsumer()
    {
        const string email = "recovery_concurrent@appetee.test";
        await SignUpAsync(email, "OriginalPassword123!");
        var token = await IssueTokenAsync(email);
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync(
                "/api/auth/password-recovery/confirm",
                new PasswordRecoveryConfirmRequest(token, "ConcurrentPassword123!")),
            secondClient.PostAsJsonAsync(
                "/api/auth/password-recovery/confirm",
                new PasswordRecoveryConfirmRequest(token, "ConcurrentPassword123!")));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            var rejected = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.BadRequest);
            await AssertInvalidTokenAsync(rejected);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private async Task SignUpAsync(string email, string password)
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new SignUpRequest(
                $"user_{Guid.NewGuid():N}"[..20],
                email,
                password));
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> RequestRecoveryAsync(string email) =>
        await Client.PostAsJsonAsync(
            "/api/auth/password-recovery/request",
            new PasswordRecoveryRequest(email));

    private async Task<string> IssueTokenAsync(string email)
    {
        using var response = await RequestRecoveryAsync(email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return Assert.Single(Factory.RecoveryEmailSender.Deliveries).Token;
    }

    private async Task AssertLoginAsync(
        string email,
        string password,
        HttpStatusCode expectedStatus)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private static async Task AssertInvalidTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemDetailsAsync();
        Assert.NotNull(problem);
        Assert.Equal(
            "The password recovery link is invalid or expired.",
            problem!.Detail);
        var code = Assert.IsType<JsonElement>(problem.Extensions["code"]);
        Assert.Equal("password_recovery_token_invalid", code.GetString());
    }
}
