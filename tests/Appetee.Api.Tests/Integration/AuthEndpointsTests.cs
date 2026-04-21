using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Api.Tests.Infrastructure;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class AuthEndpointsTests : IntegrationTestBase
{
    public AuthEndpointsTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SignUp_CreatesUser_SetsCookie_AndPersistsPreferences()
    {
        var request = new SignUpRequest(
            Username: "integration_signup",
            Email: "integration_signup@appetee.test",
            Password: "Password123!",
            DietIds: new[] { 1, 2 },
            IngredientRestrictionIds: new[] { 4 });

        var response = await Client.PostAsJsonAsync("/api/auth/sign-up", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, cookie => cookie.Contains("__Host-appetee", StringComparison.Ordinal));

        var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(authResult);

        var user = await Client.GetFromJsonAsync<UserDto>($"/api/users/{authResult!.userId}");
        Assert.NotNull(user);
        Assert.Equal(request.Username, user!.username);
        Assert.Equal(request.Email, user.email);
        Assert.Equal(new[] { 1, 2 }, user.dietIds);
        Assert.Equal(new[] { 4 }, user.ingredientRestrictionIds);
    }

    [Fact]
    public async Task SignUp_ReturnsBadRequest_WhenUsernameIsMissing()
    {
        var request = new SignUpRequest(
            Username: "",
            Email: "missing_username@appetee.test",
            Password: "Password123!");

        var response = await Client.PostAsJsonAsync("/api/auth/sign-up", request);
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Username is required.", problem!.Detail);
    }

    [Fact]
    public async Task SignUp_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var request = new SignUpRequest(
            Username: "duplicate_email",
            Email: "duplicate_email@appetee.test",
            Password: "Password123!");

        var firstResponse = await Client.PostAsJsonAsync("/api/auth/sign-up", request);
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await Client.PostAsJsonAsync("/api/auth/sign-up", request);
        var problem = await secondResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("already exists", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ReturnsCookie_ForExistingUser()
    {
        var password = "Password123!";
        var signUpRequest = new SignUpRequest(
            Username: "login_user",
            Email: "login_user@appetee.test",
            Password: password);

        var signUpResponse = await Client.PostAsJsonAsync("/api/auth/sign-up", signUpRequest);
        signUpResponse.EnsureSuccessStatusCode();

        using var loginClient = CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(signUpRequest.Email, password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, cookie => cookie.Contains("__Host-appetee", StringComparison.Ordinal));

        var sessionResponse = await loginClient.GetAsync("/api/auth/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<UserSessionDto>();

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("login_user", session!.Username);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        var password = "Password123!";
        var signUpRequest = new SignUpRequest(
            Username: "wrong_password_user",
            Email: "wrong_password_user@appetee.test",
            Password: password);

        var signUpResponse = await Client.PostAsJsonAsync("/api/auth/sign-up", signUpRequest);
        signUpResponse.EnsureSuccessStatusCode();

        using var loginClient = CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(signUpRequest.Email, "WrongPassword123!"));
        var problem = await loginResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Invalid credentials.", problem!.Detail);
    }

    [Fact]
    public async Task Session_ReturnsUnauthorized_WhenCookieIsMissing()
    {
        var response = await Client.GetAsync("/api/auth/session");
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("authentication cookie", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Session_ReturnsCurrentUser_WhenCookieIsPresent()
    {
        var (authClient, authResult) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/auth/session");
        var session = await response.Content.ReadFromJsonAsync<UserSessionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        Assert.Equal(authResult.userId, session!.userId);
        Assert.Equal(authResult.userName, session.Username);
    }

    [Fact]
    public async Task Logout_ClearsCookie_AndInvalidatesSession()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var logoutResponse = await client.PostAsync("/api/auth/logout", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.True(logoutResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, cookie => cookie.Contains("__Host-appetee", StringComparison.Ordinal));

        var sessionResponse = await client.GetAsync("/api/auth/session");
        var problem = await sessionResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("authentication cookie", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
