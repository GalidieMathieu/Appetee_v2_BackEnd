using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Api.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class UsersEndpointsTests : IntegrationTestBase
{
    public UsersEndpointsTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetById_ReturnsSeededUser_WithPreferences()
    {
        var user = await Client.GetFromJsonAsync<UserDto>("/api/users/1");

        Assert.NotNull(user);
        Assert.Equal(1, user!.id);
        Assert.Equal("ava_seed", user.username);
        Assert.Equal("ava.seed@appetee.test", user.email);
        Assert.Equal(new[] { 1, 2 }, user.dietIds);
        Assert.Equal(new[] { 4 }, user.ingredientRestrictionIds);
    }

    [Fact]
    public async Task GetById_ReturnsBadRequest_WhenIdIsZero()
    {
        var response = await Client.GetAsync("/api/users/0");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("id must be > 0", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var response = await Client.GetAsync("/api/users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExistsByEmail_ReturnsTrue_ForSeededUser()
    {
        var response = await Client.GetFromJsonAsync<EmailExistsResponse>("/api/users/exists-by-email?email=ava.seed@appetee.test");

        Assert.NotNull(response);
        Assert.True(response!.exists);
    }

    [Fact]
    public async Task ExistsByEmail_ValidatesTheQueryString()
    {
        var invalidResponse = await Client.GetAsync("/api/users/exists-by-email?email=not-an-email");
        var missingResponse = await Client.GetAsync("/api/users/exists-by-email?email=");

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
    }

    [Fact]
    public async Task GetMe_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsTheAuthenticatedUser()
    {
        var (authClient, authResult) = await CreateAuthenticatedClientAsync();
        using var client = authClient;

        var response = await client.GetAsync("/api/users/me");
        var user = await response.Content.ReadFromJsonAsync<UserDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(user);
        Assert.Equal(authResult.userId, user!.id);
        Assert.Equal(authResult.userName, user.username);
    }

    [Fact]
    public async Task List_ClampsTake_AndReturnsUsers()
    {
        var response = await Client.GetAsync("/api/users?skip=-5&take=0");
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"List users failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        var users = await response.Content.ReadFromJsonAsync<IReadOnlyList<UserDto>>();

        Assert.NotNull(users);
        Assert.Single(users!);
        Assert.Equal(2, users[0].id);
    }

    [Fact]
    public async Task Update_ReturnsUpdatedUser_AndPersistsUsernameAndImageUrl()
    {
        var response = await Client.PutAsJsonAsync("/api/users/1", new UpdateUserRequest("updated_ava", "https://cdn.test/users/ava-updated.png"));
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        var profile = await Factory.Database.QuerySingleOrDefaultAsync<UserProfileRow>(
            "SELECT username AS Username, image_url AS ImageUrl FROM users WHERE id = @id;",
            new { id = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(user);
        Assert.NotNull(profile);
        Assert.Equal("updated_ava", profile!.Username);
        Assert.Equal("https://cdn.test/users/ava-updated.png", profile.ImageUrl);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenBodyIsNull()
    {
        using var content = JsonContent.Create<object?>(null);
        var response = await Client.PutAsync("/api/users/1", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenImageUrlIsTooLong()
    {
        var tooLongUrl = $"https://cdn.test/{new string('a', 260)}";
        var response = await Client.PutAsJsonAsync("/api/users/1", new UpdateUserRequest(null, tooLongUrl));
        var problem = await response.ReadProblemDetailsAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("ImageUrl too long", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RemovesTheUser()
    {
        var response = await Client.DeleteAsync("/api/users/2");
        var deletedUser = await Factory.Database.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM users WHERE id = @id;",
            new { id = 2 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var response = await Client.DeleteAsync("/api/users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record EmailExistsResponse(bool exists);

    private sealed record UserProfileRow(string? Username, string? ImageUrl);
}
