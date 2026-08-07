using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Abstractions.Users;
using Appetee.Application.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Appetee.Api.Tests.Integration;

public sealed class UserRepositoryTests : IntegrationTestBase
{
    public UserRepositoryTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task UpdateCurrentProfileAsync_OnlyUpdatesTheScopedAccount()
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IUserRepository>();

        await repository.UpdateCurrentProfileAsync(
            1,
            new UpdateCurrentUserProfileRequest(
                "repository_updated",
                "https://cdn.test/users/repository.png"),
            CancellationToken.None);

        var userA = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = 1;");
        var userB = await Factory.Database.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT username AS Username, image_url AS ImageUrl " +
            "FROM users WHERE id = 2;");

        Assert.NotNull(userA);
        Assert.Equal("repository_updated", userA!.Username);
        Assert.Equal(
            "https://cdn.test/users/repository.png",
            userA.ImageUrl);
        Assert.NotNull(userB);
        Assert.Equal("noah_seed", userB!.Username);
        Assert.Equal("https://cdn.test/users/noah.png", userB.ImageUrl);
    }

    private sealed record UserRow(string Username, string? ImageUrl);
}
