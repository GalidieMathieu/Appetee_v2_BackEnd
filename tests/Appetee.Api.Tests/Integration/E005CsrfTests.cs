using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Dtos;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class E005CsrfTests : IntegrationTestBase
{
    private const string HeaderName = "X-CSRF-TOKEN";
    private const string Origin = "https://localhost:4200";

    public E005CsrfTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    private HttpClient RawClient() => Factory.CreateClient(new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    [Fact]
    public async Task Bootstrap_IsAnonymousNoStoreAndDoesNotCreateFeatureRows()
    {
        using var client = RawClient();
        using var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<Token>();
        Assert.NotNull(token);
        Assert.Equal(HeaderName, token.HeaderName);
        Assert.False(string.IsNullOrEmpty(token.RequestToken));
        Assert.True(response.Headers.CacheControl!.NoStore);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-appetee-csrf=", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings;"));
    }

    [Fact]
    public async Task MissingToken_RejectsAnonymousLogoutBeforeExecution()
    {
        using var client = RawClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Origin", Origin);
        using var response = await client.SendAsync(request);
        await AssertCsrfRejectedAsync(response);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private static async Task<Token> BootstrapAsync(HttpClient client) =>
        await client.GetFromJsonAsync<Token>("/api/auth/csrf")
        ?? throw new InvalidOperationException("Missing CSRF token response.");

    [Theory]
    [InlineData("POST", "/api/auth/logout", "json")]
    [InlineData("PUT", "/api/users/me", "json")]
    [InlineData("PATCH", "/api/users/me", "json")]
    [InlineData("DELETE", "/api/users/me", "json")]
    [InlineData("POST", "/api/auth/logout", "form")]
    [InlineData("POST", "/api/auth/logout", "multipart")]
    public async Task MissingHeader_BlocksUnsafeMethodsAndBrowserBodyTypesWithoutChangingUser(
        string method, string path, string bodyType)
    {
        using var client = RawClient();
        await SignUpRawAsync(client);
        var before = await client.GetStringAsync("/api/users/me");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("Origin", Origin);
        request.Content = bodyType switch
        {
            "form" => new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = "unwanted", ["__RequestVerificationToken"] = (await BootstrapAsync(client)).RequestToken,
            }),
            "multipart" => new MultipartFormDataContent { { new StringContent("unwanted"), "username" } },
            _ => JsonContent.Create(new { username = "unwanted" }),
        };
        using var response = await client.SendAsync(request);
        await AssertCsrfRejectedAsync(response);
        Assert.Equal(before, await client.GetStringAsync("/api/users/me"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings;"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("https://evil.example")]
    [InlineData("https://localhost:4200.evil.example")]
    [InlineData("https://localhost:4201")]
    [InlineData("http://localhost:4200")]
    [InlineData("https://localhost:4200/path")]
    [InlineData("https://localhost:4200@evil.example")]
    [InlineData("https://localhost:4200, https://evil.example")]
    public async Task MissingOrUntrustedOrigin_IsRejectedEvenWithValidToken(string? origin)
    {
        using var client = RawClient();
        await SignUpRawAsync(client);
        var before = await client.GetStringAsync("/api/users/me");
        var token = await BootstrapAsync(client);
        using var request = Request(HttpMethod.Put, "/api/users/me", token.RequestToken, origin);
        request.Content = JsonContent.Create(new { username = "unwanted" });
        using var response = await client.SendAsync(request);
        await AssertForbiddenAsync(response);
        Assert.Equal(before, await client.GetStringAsync("/api/users/me"));
    }

    [Theory]
    [InlineData("Sec-Fetch-Mode", "navigate")]
    [InlineData("Sec-Fetch-Mode", "no-cors")]
    [InlineData("Sec-Fetch-Dest", "iframe")]
    [InlineData("Sec-Fetch-Site", "unknown")]
    public async Task UnsafeFetchMetadata_IsRejectedBeforeLogout(string name, string value)
    {
        using var client = RawClient();
        await SignUpRawAsync(client);
        using var request = Request(HttpMethod.Post, "/api/auth/logout",
            (await BootstrapAsync(client)).RequestToken);
        request.Headers.Add(name, value);
        using var response = await client.SendAsync(request);
        await AssertForbiddenAsync(response);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Theory]
    [InlineData("https://localhost", "same-origin")]
    [InlineData(Origin, "cross-site")]
    public async Task ValidTokenAndTrustedFetch_UpdateOnlyIntendedProfile(string origin, string site)
    {
        using var client = RawClient();
        await SignUpRawAsync(client);
        using var request = Request(HttpMethod.Put, "/api/users/me",
            (await BootstrapAsync(client)).RequestToken, origin);
        request.Headers.Add("Sec-Fetch-Site", site);
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        request.Headers.Add("Sec-Fetch-Dest", "empty");
        request.Content = JsonContent.Create(new { username = "Intended name" });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("Intended name",
            (await client.GetFromJsonAsync<CurrentUserProfileDto>("/api/users/me"))!.Username);
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("duplicate")]
    [InlineData("foreign-cookie")]
    public async Task InvalidOrMismatchedToken_LeavesSessionAndProfileUnchanged(string kind)
    {
        using var client = RawClient();
        await SignUpRawAsync(client);
        var token = (await BootstrapAsync(client)).RequestToken;
        using var other = RawClient();
        if (kind == "foreign-cookie") token = (await BootstrapAsync(other)).RequestToken;
        if (kind == "tampered") token = "not-a-token";
        using var request = Request(HttpMethod.Post, "/api/auth/logout", token);
        if (kind == "duplicate") request.Headers.Add(HeaderName, token);
        using var response = await client.SendAsync(request);
        await AssertCsrfRejectedAsync(response);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task TokenWithoutCookie_IsRejected()
    {
        using var original = RawClient();
        using var noCookies = RawClient();
        using var request = Request(HttpMethod.Post, "/api/auth/logout",
            (await BootstrapAsync(original)).RequestToken);
        using var response = await noCookies.SendAsync(request);
        await AssertCsrfRejectedAsync(response);
    }

    [Theory]
    [InlineData("/api/admin/recipe-details", "recipes")]
    [InlineData("/api/admin/ingredient-details", "ingredients")]
    public async Task MultipartUpload_RejectsBeforePersistingAnything(string path, string table)
    {
        using var client = RawClient();
        var email=await SignUpRawAsync(client);
        var userId=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM users WHERE email=@Email;",new { Email=email });
        await new Appetee.DatabaseMigrator.PermissionOperator(Factory.Database.ConnectionString)
            .ChangeAsync(userId,"Catalog.Write",true,"synthetic-csrf-operator",null,default);
        using (var loginRequest=Request(HttpMethod.Post,"/api/auth/login",(await BootstrapAsync(client)).RequestToken))
        {
            loginRequest.Content=JsonContent.Create(new LoginRequest(email,"Password123!"));
            using var login=await client.SendAsync(loginRequest);
            Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        }
        // Table names are fixed test data, not request inputs.
        var before = await Factory.Database.QuerySingleOrDefaultAsync<int>($"SELECT COUNT(*) FROM {table};");
        using var request = Request(HttpMethod.Post, path);
        request.Content = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "ImageFile", "unwanted.png" },
            { new StringContent("unwanted"), "Name" },
        };
        using var response = await client.SendAsync(request);
        await AssertCsrfRejectedAsync(response);
        Assert.Equal(before, await Factory.Database.QuerySingleOrDefaultAsync<int>($"SELECT COUNT(*) FROM {table};"));
    }

    [Fact]
    public async Task PreviousUsersToken_WithSameAntiforgeryCookie_CannotAuthorizeNextUser()
    {
        using var client = RawClient();
        await SignUpRawAsync(client);
        var firstUsersToken = await BootstrapAsync(client);
        using (var logout = await client.SendAsync(Request(
            HttpMethod.Post, "/api/auth/logout", firstUsersToken.RequestToken)))
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await SignUpRawAsync(client);
        using var staleRequest = Request(HttpMethod.Put, "/api/users/me", firstUsersToken.RequestToken);
        staleRequest.Content = JsonContent.Create(new { username = "unwanted" });
        using var stale = await client.SendAsync(staleRequest);
        await AssertCsrfRejectedAsync(stale);
        using var freshRequest = Request(HttpMethod.Put, "/api/users/me", (await BootstrapAsync(client)).RequestToken);
        freshRequest.Content = JsonContent.Create(new { username = "Intended second user" });
        using var fresh = await client.SendAsync(freshRequest);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Fact]
    public async Task TrustedCrossSiteMultipart_WithValidToken_PreservesUploadFlow()
    {
        using var client = RawClient();
        var email = await SignUpRawAsync(client);
        var userId=await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM users WHERE email=@Email;",new { Email=email });
        await new Appetee.DatabaseMigrator.PermissionOperator(Factory.Database.ConnectionString)
            .ChangeAsync(userId,"Catalog.Write",true,"synthetic-csrf-operator",null,default);
        using (var loginRequest=Request(HttpMethod.Post,"/api/auth/login",(await BootstrapAsync(client)).RequestToken))
        {
            loginRequest.Content=JsonContent.Create(new Appetee.Application.Requests.Auth.LoginRequest(email,"Password123!"));
            using var login=await client.SendAsync(loginRequest);
            Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        }
        var before = await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM ingredients;");
        using var request = Request(HttpMethod.Post, "/api/admin/ingredient-details",
            (await BootstrapAsync(client)).RequestToken);
        request.Headers.Add("Sec-Fetch-Site", "cross-site");
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        request.Headers.Add("Sec-Fetch-Dest", "empty");
        request.Content = MultipartContentBuilder.CreateIngredientRequest(name: "Intended CSRF test ingredient");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 1, await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM ingredients;"));
    }

    [Fact]
    public async Task IdentityTransition_RequiresFreshTokenAfterLoginAndLogout()
    {
        using var client = RawClient();
        var anonymous = await BootstrapAsync(client);
        var email = await SignUpRawAsync(client, anonymous);
        using (var staleLogout = await client.SendAsync(Request(
            HttpMethod.Post, "/api/auth/logout", anonymous.RequestToken)))
            await AssertCsrfRejectedAsync(staleLogout);
        var signedIn = await BootstrapAsync(client);
        using (var logout = await client.SendAsync(Request(
            HttpMethod.Post, "/api/auth/logout", signedIn.RequestToken)))
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var staleLoginRequest = Request(HttpMethod.Post, "/api/auth/login", signedIn.RequestToken))
        {
            staleLoginRequest.Content = JsonContent.Create(new LoginRequest(email, "Password123!"));
            using var staleLogin = await client.SendAsync(staleLoginRequest);
            await AssertCsrfRejectedAsync(staleLogin);
        }
        using var loginRequest = Request(HttpMethod.Post, "/api/auth/login",
            (await BootstrapAsync(client)).RequestToken);
        loginRequest.Content = JsonContent.Create(new LoginRequest(email, "Password123!"));
        using var login = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Bootstrap_RejectsHostileOriginAndPreflightDoesNotNeedToken()
    {
        using var client = RawClient();
        using var hostileRequest = Request(HttpMethod.Get, "/api/auth/csrf", null, "https://evil.example");
        using var hostile = await client.SendAsync(hostileRequest);
        await AssertForbiddenAsync(hostile);
        Assert.False(hostile.Headers.Contains("Set-Cookie"));
        using var preflight = Request(HttpMethod.Options, "/api/auth/logout");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", HeaderName);
        using var response = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.DoesNotContain("*", response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OpenApi_DescribesBootstrapAndUnsafeHeadersWithoutChangingReads()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/auth/csrf").TryGetProperty("get", out _));
        var parameters = paths.GetProperty("/api/users/me").GetProperty("put").GetProperty("parameters");
        foreach (var name in new[] { HeaderName, "Origin" })
            Assert.Contains(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() == name
                && p.GetProperty("required").GetBoolean());
        var get = paths.GetProperty("/api/users/me").GetProperty("get");
        Assert.False(get.TryGetProperty("parameters", out var readParameters)
            && readParameters.EnumerateArray().Any(p => p.GetProperty("name").GetString() == HeaderName));
    }

    [Fact]
    public async Task AnonymousProfileResponse_Retains401AndIsNotCached()
    {
        using var client = RawClient();
        using var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path,
        string? token = null, string? origin = Origin)
    {
        var request = new HttpRequestMessage(method, path);
        if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
        if (token is not null) request.Headers.TryAddWithoutValidation(HeaderName, token);
        return request;
    }

    private static async Task<string> SignUpRawAsync(HttpClient client, Token? token = null)
    {
        token ??= await BootstrapAsync(client);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"csrf_{suffix}@appetee.test";
        using var request = Request(HttpMethod.Post, "/api/auth/sign-up", token.RequestToken);
        request.Content = JsonContent.Create(new SignUpRequest($"Csrf_{suffix}", email, "Password123!"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return email;
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    private static async Task AssertCsrfRejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("csrf_invalid", ((JsonElement)problem.Extensions["code"]!).GetString());
        Assert.False(string.IsNullOrEmpty(((JsonElement)problem.Extensions["traceId"]!).GetString()));
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    private sealed record Token(string RequestToken, string HeaderName);
}
