using Appetee.Application.Dtos;
using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class AuthEndpointsTests : IntegrationTestBase
{
    public AuthEndpointsTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public void CookieAuthentication_UsesSevenDaySlidingTicketLifetime()
    {
        var options = Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal(TimeSpan.FromDays(7), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }

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

        var profile = await Client.GetFromJsonAsync<CurrentUserProfileDto>(
            "/api/users/me");
        var storedUser = await Factory.Database
            .QuerySingleOrDefaultAsync<SignUpUserRow>(
                "SELECT username AS Username, email AS Email " +
                "FROM users WHERE id = @id;",
                new { id = authResult!.userId });
        var dietCount = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM user_diets WHERE user_id = @id;",
            new { id = authResult.userId });
        var restrictionCount = await Factory.Database
            .QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM user_ingredient_restrictions " +
                "WHERE user_id = @id;",
                new { id = authResult.userId });

        Assert.NotNull(profile);
        Assert.Equal(request.Username, profile!.Username);
        Assert.NotNull(storedUser);
        Assert.Equal(request.Username, storedUser!.Username);
        Assert.Equal(request.Email, storedUser.Email);
        Assert.Equal(2, dietCount);
        Assert.Equal(1, restrictionCount);
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
    public async Task ExistsByEmail_ReturnsTrueWithoutAuthentication_ForExistingUser()
    {
        using var response = await Client.GetAsync(
            "/api/auth/exists-by-email?email=ava.seed@appetee.test");
        var result = await response.Content
            .ReadFromJsonAsync<EmailExistsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result!.Exists);
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsFalseWithoutAuthentication_ForNewEmail()
    {
        using var response = await Client.GetAsync(
            "/api/auth/exists-by-email?email=new.user@appetee.test");
        var result = await response.Content
            .ReadFromJsonAsync<EmailExistsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result!.Exists);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task ExistsByEmail_ReturnsProblemDetails400_ForInvalidEmail(
        string email)
    {
        using var response = await Client.GetAsync(
            $"/api/auth/exists-by-email?email={Uri.EscapeDataString(email)}");
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
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
        var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(signUpRequest.Email, password, RememberMe: true));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        var authCookie = SetCookieHeaderValue.Parse(
            Assert.Single(cookies!, cookie =>
                cookie.Contains("__Host-appetee", StringComparison.Ordinal)));
        Assert.NotNull(authCookie.Expires);
        Assert.InRange(
            authCookie.Expires!.Value - DateTimeOffset.UtcNow,
            TimeSpan.FromDays(6).Add(TimeSpan.FromHours(23)),
            TimeSpan.FromDays(7).Add(TimeSpan.FromHours(1)));

        var sessionResponse = await loginClient.GetAsync("/api/auth/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<UserSessionDto>();

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("login_user", session!.Username);
    }

    [Fact]
    public async Task Login_UsesBrowserSessionCookie_WhenRememberMeIsFalse()
    {
        var password = "Password123!";
        var signUpRequest = new SignUpRequest(
            Username: "browser_session_user",
            Email: "browser_session_user@appetee.test",
            Password: password);

        var signUpResponse = await Client.PostAsJsonAsync("/api/auth/sign-up", signUpRequest);
        signUpResponse.EnsureSuccessStatusCode();

        using var loginClient = CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(signUpRequest.Email, password, RememberMe: false));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        var authCookie = SetCookieHeaderValue.Parse(
            Assert.Single(cookies!, cookie =>
                cookie.Contains("__Host-appetee", StringComparison.Ordinal)));
        Assert.Null(authCookie.Expires);
        Assert.Null(authCookie.MaxAge);
    }

    [Fact]
    public async Task Session_RenewsRememberedCookie_AfterHalfTheIdleWindow()
    {
        var timeProvider = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateTimeControlledFactory(timeProvider);
        using var client = await CreateLoggedInClientAsync(
            factory,
            "sliding_session_user",
            "sliding_session_user@appetee.test",
            rememberMe: true);

        timeProvider.Advance(TimeSpan.FromDays(4));
        using var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var renewedCookie = SetCookieHeaderValue.Parse(
            Assert.Single(cookies!, cookie =>
                cookie.Contains("__Host-appetee", StringComparison.Ordinal)));
        Assert.NotNull(renewedCookie.Expires);
        Assert.InRange(
            renewedCookie.Expires!.Value - timeProvider.GetUtcNow(),
            TimeSpan.FromDays(7).Subtract(TimeSpan.FromMinutes(1)),
            TimeSpan.FromDays(7).Add(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Session_ReturnsExpiredOutcome_AfterSevenDaysWithoutActivity()
    {
        var timeProvider = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateTimeControlledFactory(timeProvider);
        using var client = await CreateLoggedInClientAsync(
            factory,
            "idle_expiry_user",
            "idle_expiry_user@appetee.test",
            rememberMe: true);

        timeProvider.Advance(TimeSpan.FromDays(7).Add(TimeSpan.FromSeconds(1)));
        using var response = await client.GetAsync("/api/auth/session");
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSessionExpired(problem);
    }

    [Fact]
    public async Task Session_ReturnsExpiredOutcome_AtThirtyDayAbsoluteLimit()
    {
        var timeProvider = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateTimeControlledFactory(timeProvider);
        using var client = await CreateLoggedInClientAsync(
            factory,
            "absolute_expiry_user",
            "absolute_expiry_user@appetee.test",
            rememberMe: true);

        for (var day = 4; day <= 28; day += 4)
        {
            timeProvider.Advance(TimeSpan.FromDays(4));
            using var activeResponse = await client.GetAsync("/api/auth/session");
            Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        }

        timeProvider.Advance(TimeSpan.FromDays(2));
        using var expiredResponse = await client.GetAsync("/api/auth/session");
        var problem = await expiredResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
        AssertSessionExpired(problem);
    }

    [Fact]
    public async Task Logout_EndsOnlyCurrentBrowserSession()
    {
        const string email = "multi_browser_user@appetee.test";
        const string password = "Password123!";
        var signUpRequest = new SignUpRequest(
            Username: "multi_browser_user",
            Email: email,
            Password: password);

        using var setupClient = CreateClient();
        using var signUpResponse = await setupClient.PostAsJsonAsync(
            "/api/auth/sign-up",
            signUpRequest);
        signUpResponse.EnsureSuccessStatusCode();

        using var firstBrowser = CreateClient();
        using var secondBrowser = CreateClient();
        using var firstLogin = await firstBrowser.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password, RememberMe: true));
        using var secondLogin = await secondBrowser.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password, RememberMe: true));
        firstLogin.EnsureSuccessStatusCode();
        secondLogin.EnsureSuccessStatusCode();

        using var logoutResponse = await firstBrowser.PostAsync(
            "/api/auth/logout",
            new StringContent(string.Empty));
        using var firstSession = await firstBrowser.GetAsync("/api/auth/session");
        using var secondSession = await secondBrowser.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, firstSession.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondSession.StatusCode);
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
        Assert.Equal("Invalid email or password.", problem!.Detail);
        Assert.False(loginResponse.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_ReturnsSameGenericUnauthorized_ForUnknownEmail()
    {
        using var loginClient = CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown@appetee.test", "Password123!", RememberMe: false));
        var problem = await loginResponse.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Invalid email or password.", problem!.Detail);
        Assert.False(loginResponse.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_ReturnsStableVerificationRequiredOutcome_WithoutCookie()
    {
        using var pendingFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthQueries>();
                services.AddScoped<IAuthQueries, PendingVerificationAuthQueries>();
            });
        });
        using var pendingClient = pendingFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        var response = await pendingClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("pending@appetee.test", "Password123!", RememberMe: false));
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(problem);
        var code = Assert.IsType<JsonElement>(problem!.Extensions["code"]);
        Assert.Equal(
            "email_verification_required",
            code.GetString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
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

    [Fact]
    public async Task Logout_EndsOnlyTheCurrentBrowserSession()
    {
        var password = "Password123!";
        var signUpRequest = new SignUpRequest(
            Username: "multiple_sessions_user",
            Email: "multiple_sessions_user@appetee.test",
            Password: password);

        var signUpResponse = await Client.PostAsJsonAsync("/api/auth/sign-up", signUpRequest);
        signUpResponse.EnsureSuccessStatusCode();

        using var firstBrowser = CreateClient();
        using var secondBrowser = CreateClient();
        var login = new LoginRequest(signUpRequest.Email, password, RememberMe: true);

        (await firstBrowser.PostAsJsonAsync("/api/auth/login", login)).EnsureSuccessStatusCode();
        (await secondBrowser.PostAsJsonAsync("/api/auth/login", login)).EnsureSuccessStatusCode();

        var logoutResponse = await firstBrowser.PostAsync(
            "/api/auth/logout",
            new StringContent(string.Empty));
        var firstSession = await firstBrowser.GetAsync("/api/auth/session");
        var secondSession = await secondBrowser.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, firstSession.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondSession.StatusCode);
    }

    private sealed record SignUpUserRow(string Username, string Email);

    private WebApplicationFactory<Program> CreateTimeControlledFactory(
        AdjustableTimeProvider timeProvider) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(timeProvider);
            });
        });

    private static async Task<HttpClient> CreateLoggedInClientAsync(
        WebApplicationFactory<Program> factory,
        string username,
        string email,
        bool rememberMe)
    {
        const string password = "Password123!";
        using var setupClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        using var signUpResponse = await setupClient.PostAsJsonAsync(
            "/api/auth/sign-up",
            new SignUpRequest(username, email, password));
        signUpResponse.EnsureSuccessStatusCode();

        var loginClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        using var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password, rememberMe));
        loginResponse.EnsureSuccessStatusCode();

        return loginClient;
    }

    private static void AssertSessionExpired(Microsoft.AspNetCore.Mvc.ProblemDetails? problem)
    {
        Assert.NotNull(problem);
        Assert.Equal(
            "Your session expired. Please log in again.",
            problem!.Detail);
        var code = Assert.IsType<JsonElement>(problem.Extensions["code"]);
        Assert.Equal("session_expired", code.GetString());
    }

    private sealed class PendingVerificationAuthQueries : IAuthQueries
    {
        public Task<LoginAttempt> LoginAsync(LoginRequest user, CancellationToken ct) =>
            Task.FromResult(LoginAttempt.EmailVerificationRequired());

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) =>
            Task.FromResult(true);
    }
}
