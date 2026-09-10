using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class UsersEndpointsTests : IntegrationTestBase
{
    private const string DisabledRouteDetail =
        "This account route is not available.";

    public UsersEndpointsTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    public static TheoryData<string, string> AnonymousAccountRoutes => new()
    {
        { HttpMethod.Get.Method, "/api/users" },
        { HttpMethod.Get.Method, "/api/users/1" },
        {
            HttpMethod.Get.Method,
            "/api/users/exists-by-email?email=ava.seed@appetee.test"
        },
        { HttpMethod.Put.Method, "/api/users/1" },
        { HttpMethod.Put.Method, "/api/users/me" },
        { HttpMethod.Delete.Method, "/api/users/1" },
    };

    [Theory]
    [MemberData(nameof(AnonymousAccountRoutes))]
    public async Task AccountRoutes_ReturnProblemDetails401_WhenAnonymous(
        string method,
        string path)
    {
        using var request = CreateRequest(method, path);
        using var response = await Client.SendAsync(request);

        var problem = await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized");

        Assert.Equal(
            "Authentication is required to access this resource.",
            problem.Detail);
    }

    [Fact]
    public async Task GetMe_ReturnsProblemDetails401_WhenAnonymous()
    {
        using var response = await Client.GetAsync("/api/users/me");

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Unauthorized");
    }

    [Fact]
    public async Task GetMe_ReturnsTheAuthenticatedUser()
    {
        var (authClient, authResult) =
            await CreateAuthenticatedClientAsync();
        using var client = authClient;

        using var response = await client.GetAsync("/api/users/me");
        var body = await response.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<CurrentUserProfileDto>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(body);
        var properties = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal(authResult.userName, profile!.Username);
        Assert.Null(profile.ImageUrl);
        Assert.Equal(new[] { "imageUrl", "username" }, properties);
    }

    [Fact]
    public async Task UpdateMe_UpdatesOnlyTheAuthenticatedAccount()
    {
        var (userAClient, userA) = await CreateAuthenticatedClientAsync(
            username: "phase2_user_a",
            email: "phase2_user_a@appetee.test");
        var (userBClient, userB) = await CreateAuthenticatedClientAsync(
            username: "phase2_user_b",
            email: "phase2_user_b@appetee.test");
        using var clientA = userAClient;
        using var clientB = userBClient;

        var request = new UpdateCurrentUserProfileRequest(
            "  phase2_user_a_updated  ",
            "https://cdn.test/users/phase2-a.png");
        using var response = await clientA.PutAsJsonAsync(
            "/api/users/me",
            request);
        var profile = await response.Content
            .ReadFromJsonAsync<CurrentUserProfileDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal("phase2_user_a_updated", profile!.Username);
        Assert.Equal(
            "https://cdn.test/users/phase2-a.png",
            profile.ImageUrl);

        var storedA = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT id AS Id, username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = @id;",
            new { id = userA.userId });
        var storedB = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT id AS Id, username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = @id;",
            new { id = userB.userId });

        Assert.NotNull(storedA);
        Assert.Equal("phase2_user_a_updated", storedA!.Username);
        Assert.Equal(
            "https://cdn.test/users/phase2-a.png",
            storedA.ImageUrl);
        Assert.NotNull(storedB);
        Assert.Equal("phase2_user_b", storedB!.Username);
        Assert.Null(storedB.ImageUrl);
    }

    [Theory]
    [InlineData(null, null, "At least one profile field is required.")]
    [InlineData("   ", null, "Username is required.")]
    public async Task UpdateMe_ReturnsProblemDetails400_ForInvalidInput(
        string? username,
        string? imageUrl,
        string expectedDetail)
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        using var response = await client.PutAsJsonAsync(
            "/api/users/me",
            new UpdateCurrentUserProfileRequest(username, imageUrl));
        var problem = await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "Bad Request");

        Assert.Equal(expectedDetail, problem.Detail);
    }

    [Fact]
    public async Task UpdateMe_ReturnsProblemDetails400_WhenImageUrlIsTooLong()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        using var response = await client.PutAsJsonAsync(
            "/api/users/me",
            new UpdateCurrentUserProfileRequest(
                null,
                $"https://cdn.test/{new string('a', 260)}"));
        var problem = await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "Bad Request");

        Assert.Contains("ImageUrl too long", problem.Detail);
    }

    [Fact]
    public async Task UpdateMe_RejectsOverpostingAndIdentityTampering()
    {
        var (userAClient, userA) = await CreateAuthenticatedClientAsync(
            username: "phase2_overpost_a",
            email: "phase2_overpost_a@appetee.test");
        var (userBClient, userB) = await CreateAuthenticatedClientAsync(
            username: "phase2_overpost_b",
            email: "phase2_overpost_b@appetee.test");
        using var clientA = userAClient;
        using var clientB = userBClient;

        using var response = await clientA.PutAsJsonAsync(
            "/api/users/me",
            new
            {
                username = "tampered",
                userId = userB.userId,
                ownerId = userB.userId,
                email = "tampered@appetee.test",
                dietIds = new[] { 3 }
            });

        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal(
            "One or more validation errors occurred.",
            problem.Title);
        Assert.True(problem.Extensions.ContainsKey("errors"));

        var storedA = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT id AS Id, username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = @id;",
            new { id = userA.userId });
        var storedB = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT id AS Id, username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = @id;",
            new { id = userB.userId });

        Assert.Equal("phase2_overpost_a", storedA!.Username);
        Assert.Equal("phase2_overpost_b", storedB!.Username);
    }

    [Fact]
    public async Task LegacyReads_ReturnTheSame404_ForOwnCrossUserAndMissingIds()
    {
        var (userAClient, userA) = await CreateAuthenticatedClientAsync(
            username: "phase1_user_a",
            email: "phase1_user_a@appetee.test");
        var (userBClient, userB) = await CreateAuthenticatedClientAsync(
            username: "phase1_user_b",
            email: "phase1_user_b@appetee.test");
        using var clientA = userAClient;
        using var clientB = userBClient;

        var responses = new[]
        {
            await clientA.GetAsync($"/api/users/{userA.userId}"),
            await clientA.GetAsync($"/api/users/{userB.userId}"),
            await clientB.GetAsync($"/api/users/{userA.userId}"),
            await clientA.GetAsync("/api/users/999999"),
        };

        try
        {
            foreach (var response in responses)
            {
                var problem = await AssertProblemDetailsAsync(
                    response,
                    HttpStatusCode.NotFound,
                    "Not Found");

                Assert.Equal(DisabledRouteDetail, problem.Detail);
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task LegacyCrossUserMutations_Return404_AndDoNotChangeTarget()
    {
        var (userAClient, _) = await CreateAuthenticatedClientAsync(
            username: "phase1_mutator",
            email: "phase1_mutator@appetee.test");
        var (userBClient, userB) = await CreateAuthenticatedClientAsync(
            username: "phase1_target",
            email: "phase1_target@appetee.test");
        using var clientA = userAClient;
        using var clientB = userBClient;

        using var updateResponse = await clientA.PutAsJsonAsync(
            $"/api/users/{userB.userId}",
            new
            {
                username = "compromised",
                imageUrl = "https://cdn.test/compromised.png"
            });
        using var deleteResponse = await clientA.DeleteAsync(
            $"/api/users/{userB.userId}");

        await AssertProblemDetailsAsync(
            updateResponse,
            HttpStatusCode.NotFound,
            "Not Found");
        await AssertProblemDetailsAsync(
            deleteResponse,
            HttpStatusCode.NotFound,
            "Not Found");

        var target = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT id AS Id, username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = @id;",
            new { id = userB.userId });

        Assert.NotNull(target);
        Assert.Equal("phase1_target", target!.Username);

        // The target's existing authenticated session is still usable too.
        using var meResponse = await clientB.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task GeneralUserDiscoveryRoutes_Return404_WhenAuthenticated()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        using var listResponse = await client.GetAsync("/api/users");
        using var emailResponse = await client.GetAsync(
            "/api/users/exists-by-email?email=ava.seed@appetee.test");

        var listProblem = await AssertProblemDetailsAsync(
            listResponse,
            HttpStatusCode.NotFound,
            "Not Found");
        var emailProblem = await AssertProblemDetailsAsync(
            emailResponse,
            HttpStatusCode.NotFound,
            "Not Found");

        Assert.Equal(DisabledRouteDetail, listProblem.Detail);
        Assert.Equal(DisabledRouteDetail, emailProblem.Detail);
    }

    [Fact]
    public async Task OpenApi_OnlyPublishesTheCurrentUserAccountRoute()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/users/me", out _));
        Assert.False(paths.TryGetProperty("/api/users", out _));
        Assert.False(paths.TryGetProperty("/api/users/{id}", out _));
        Assert.False(
            paths.TryGetProperty("/api/users/exists-by-email", out _));
    }

    private static HttpRequestMessage CreateRequest(
        string method,
        string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method == HttpMethod.Put.Method)
        {
            request.Content = JsonContent.Create(new { username = "blocked" });
        }

        return request;
    }

    private static async Task<ProblemDetails> AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle)
    {
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem!.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));

        return problem;
    }

    private sealed record UserRow(
        int Id,
        string Username,
        string? ImageUrl = null);
}
