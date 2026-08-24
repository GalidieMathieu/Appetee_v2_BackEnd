/*
 * Purpose: Verifies login validation, outcome mapping, and cookie-issuance boundaries in AuthService.
 * Created: 2026-08-21T10:36:10-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Application.utils;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Appetee.Api.Tests.Unit;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LogInAsync_PreservesRememberMeIntent_AndSignsInAuthenticatedUser()
    {
        var queries = new StubAuthQueries(
            LoginAttempt.Authenticated(new AuthResult(42, "remembered_user")));
        var cookies = new RecordingCookieService();
        var service = CreateService(queries, cookies);
        var request = new LoginRequest(
            "  remembered@appetee.test  ",
            "Password123!",
            RememberMe: true);

        var result = await service.LogInAsync(
            new DefaultHttpContext(),
            request,
            CancellationToken.None);

        Assert.Equal("remembered@appetee.test", queries.LastRequest!.Email);
        Assert.True(queries.LastRequest!.RememberMe);
        Assert.Equal(42, result.userId);
        Assert.Equal((42, "remembered_user", true), cookies.SignedInUser);
    }

    [Fact]
    public async Task LogInAsync_MapsInvalidCredentialsToApprovedGenericMessage()
    {
        var cookies = new RecordingCookieService();
        var service = CreateService(
            new StubAuthQueries(LoginAttempt.InvalidCredentials()),
            cookies);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LogInAsync(
                new DefaultHttpContext(),
                new LoginRequest("user@appetee.test", "wrong", RememberMe: false),
                CancellationToken.None));

        Assert.Equal("Invalid email or password.", exception.Message);
        Assert.Null(cookies.SignedInUser);
    }

    [Fact]
    public async Task LogInAsync_MapsPendingVerificationToStableCode_WithoutSigningIn()
    {
        var cookies = new RecordingCookieService();
        var service = CreateService(
            new StubAuthQueries(LoginAttempt.EmailVerificationRequired()),
            cookies);

        var exception = await Assert.ThrowsAsync<EmailVerificationRequiredException>(() =>
            service.LogInAsync(
                new DefaultHttpContext(),
                new LoginRequest("pending@appetee.test", "Password123!", RememberMe: true),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("email_verification_required", exception.Code);
        Assert.Null(cookies.SignedInUser);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task LogInAsync_RejectsInvalidEmailBeforeCredentialLookup(string email)
    {
        var queries = new StubAuthQueries(
            LoginAttempt.Authenticated(new AuthResult(42, "user")));
        var service = CreateService(queries, new RecordingCookieService());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.LogInAsync(
                new DefaultHttpContext(),
                new LoginRequest(email, "Password123!", RememberMe: false),
                CancellationToken.None));

        Assert.Null(queries.LastRequest);
    }

    private static AuthService CreateService(
        IAuthQueries queries,
        IAuthCookieService cookies) =>
        new(
            new StubAuthRepository(),
            queries,
            new StubPasswordHasher(),
            cookies);

    private sealed class StubAuthQueries(LoginAttempt result) : IAuthQueries
    {
        public LoginRequest? LastRequest { get; private set; }

        public Task<LoginAttempt> LoginAsync(LoginRequest user, CancellationToken ct)
        {
            LastRequest = user;
            return Task.FromResult(result);
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class RecordingCookieService : IAuthCookieService
    {
        public (int UserId, string Username, bool RememberMe)? SignedInUser { get; private set; }

        public Task SignInAsync(
            HttpContext http,
            int userId,
            string username,
            bool rememberMe = false,
            IEnumerable<Claim>? extraClaims = null)
        {
            SignedInUser = (userId, username, rememberMe);
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext http) => Task.CompletedTask;

        public UserSessionDto? GetSession(ClaimsPrincipal user) => null;
    }

    private sealed class StubAuthRepository : IAuthRepository
    {
        public Task<int> CreateUserAsync(
            string username,
            string email,
            string passwordHash,
            IReadOnlyList<int>? dietIds,
            IReadOnlyList<int>? ingredientRestrictionIds,
            CancellationToken ct) =>
            Task.FromResult(1);
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => password;

        public bool Verify(string password, string passwordHash) =>
            password == passwordHash;
    }
}
