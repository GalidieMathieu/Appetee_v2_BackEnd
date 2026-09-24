/*
 * Purpose: Verifies validation and claim scoping in current-account application workflows.
 * Change reason: Add E-001 Phase 4 account-closure orchestration coverage.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

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
        var service = CreateService(queries, repository);

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
        var service = CreateService(queries, repository);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateCurrentProfileAsync(
                42,
                new UpdateCurrentUserProfileRequest(" ", null),
                CancellationToken.None));

        Assert.Equal("Username is required.", exception.Message);
        Assert.Null(repository.Request);
        Assert.Null(queries.CurrentUserId);
    }

    [Fact]
    public async Task CloseCurrentAccountAsync_UsesCurrentUserAndProcessesCommittedCleanup()
    {
        var closureRepository = new StubAccountClosureRepository
        {
            CleanupId = 123,
        };
        var processor = new CapturingAccountClosureProcessor();
        var service = CreateService(
            new StubUserQueries(),
            new CapturingUserRepository(),
            closureRepository,
            processor);

        var closed = await service.CloseCurrentAccountAsync(
            42,
            CancellationToken.None);

        Assert.True(closed);
        Assert.Equal(42, closureRepository.CurrentUserId);
        Assert.Equal(123, processor.CleanupId);
    }

    private static UserService CreateService(
        IUserQueries queries,
        IUserRepository repository,
        IAccountClosureRepository? closureRepository = null,
        IAccountClosureProcessor? processor = null) =>
        new(
            queries,
            repository,
            closureRepository ?? new StubAccountClosureRepository(),
            processor ?? new CapturingAccountClosureProcessor());

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

        public Task<bool> CurrentAccountExistsAsync(
            int currentUserId,
            CancellationToken ct) => Task.FromResult(true);
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


    private sealed class StubAccountClosureRepository : IAccountClosureRepository
    {
        public long? CleanupId { get; init; }

        public int? CurrentUserId { get; private set; }

        public Task<long?> CloseCurrentAccountAsync(
            int currentUserId,
            CancellationToken ct)
        {
            CurrentUserId = currentUserId;
            return Task.FromResult(CleanupId);
        }

        public Task<Appetee.Application.Models.Users.AccountClosureCleanup?> GetPendingCleanupAsync(
            long cleanupId,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<long>> ListDueCleanupIdsAsync(
            int maximumCount,
            CancellationToken ct) => throw new NotSupportedException();

        public Task MarkCleanupCompletedAsync(
            long cleanupId,
            CancellationToken ct) => throw new NotSupportedException();

        public Task MarkCleanupFailedAsync(
            long cleanupId,
            string errorCode,
            DateTimeOffset nextAttemptUtc,
            CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CapturingAccountClosureProcessor : IAccountClosureProcessor
    {
        public long? CleanupId { get; private set; }

        public Task<bool> TryProcessAsync(long cleanupId, CancellationToken ct)
        {
            CleanupId = cleanupId;
            return Task.FromResult(true);
        }

        public Task ProcessDueAsync(
            int maximumCount,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
