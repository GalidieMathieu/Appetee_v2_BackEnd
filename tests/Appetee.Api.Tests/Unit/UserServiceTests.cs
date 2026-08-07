using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.Services.Users;
using Appetee.Application.utils;

namespace Appetee.Api.Tests.Unit;

public sealed class UserServiceTests
{
    [Fact]
    public async Task UpdateCurrentProfileAsync_ScopesWriteToCurrentUserId()
    {
        var queries = new StubUserQueries
        {
            Profile = new CurrentUserProfileDto(
                "updated_user",
                "https://cdn.test/user.png")
        };
        var repository = new CapturingUserRepository();
        var service = new UserService(queries, repository);

        var result = await service.UpdateCurrentProfileAsync(
            42,
            new UpdateCurrentUserProfileRequest(
                "  updated_user  ",
                "https://cdn.test/user.png"),
            CancellationToken.None);

        Assert.Equal(42, repository.CurrentUserId);
        Assert.Equal("updated_user", repository.Request!.Username);
        Assert.Equal(42, queries.CurrentUserId);
        Assert.Equal(queries.Profile, result);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_DoesNotWriteInvalidInput()
    {
        var queries = new StubUserQueries();
        var repository = new CapturingUserRepository();
        var service = new UserService(queries, repository);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateCurrentProfileAsync(
                42,
                new UpdateCurrentUserProfileRequest(" ", null),
                CancellationToken.None));

        Assert.Equal("Username is required.", exception.Message);
        Assert.Null(repository.Request);
        Assert.Null(queries.CurrentUserId);
    }

    private sealed class StubUserQueries : IUserQueries
    {
        public CurrentUserProfileDto? Profile { get; init; }

        public int? CurrentUserId { get; private set; }

        public Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
            int currentUserId,
            CancellationToken ct)
        {
            CurrentUserId = currentUserId;
            return Task.FromResult(Profile);
        }
    }

    private sealed class CapturingUserRepository : IUserRepository
    {
        public int? CurrentUserId { get; private set; }

        public UpdateCurrentUserProfileRequest? Request { get; private set; }

        public Task UpdateCurrentProfileAsync(
            int currentUserId,
            UpdateCurrentUserProfileRequest request,
            CancellationToken ct)
        {
            CurrentUserId = currentUserId;
            Request = request;
            return Task.CompletedTask;
        }

    }
}
