/*
 * Purpose: Implements validated, claim-scoped current-account operations.
 * Change reason: Add E-001 Phase 4 durable account closure and cleanup handoff.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Application.Services.Users;

public sealed class UserService : IUserService
{
    private readonly IUserQueries _queries;
    private readonly IUserRepository _repository;
    private readonly IAccountClosureRepository _accountClosureRepository;
    private readonly IAccountClosureProcessor _accountClosureProcessor;

    public UserService(
        IUserQueries queries,
        IUserRepository repository,
        IAccountClosureRepository accountClosureRepository,
        IAccountClosureProcessor accountClosureProcessor)
    {
        _queries = queries;
        _repository = repository;
        _accountClosureRepository = accountClosureRepository;
        _accountClosureProcessor = accountClosureProcessor;
    }

    public Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct) =>
        _queries.GetCurrentProfileAsync(currentUserId, ct);

    public async Task<CurrentUserProfileDto?> UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct)
    {
        if (request.Username is null && request.ImageUrl is null)
        {
            throw new ValidationException(
                "At least one profile field is required.");
        }

        if (request.Username is not null)
        {
            var username = request.Username.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ValidationException("Username is required.");
            }

            if (username.Length > 50)
            {
                throw new ValidationException(
                    "Username too long (max 50).");
            }

            request = request with { Username = username };
        }

        if (request.ImageUrl is not null && request.ImageUrl.Length > 255)
        {
            throw new ValidationException(
                "ImageUrl too long (max 255).");
        }

        await _repository.UpdateCurrentProfileAsync(
            currentUserId,
            request,
            ct);

        return await _queries.GetCurrentProfileAsync(currentUserId, ct);
    }

    // Relational deletion commits before optional media work; the durable job makes retries safe.
    public async Task<bool> CloseCurrentAccountAsync(
        int currentUserId,
        CancellationToken ct)
    {
        var cleanupId = await _accountClosureRepository
            .CloseCurrentAccountAsync(currentUserId, ct);

        if (cleanupId is null)
        {
            return false;
        }

        await _accountClosureProcessor.TryProcessAsync(cleanupId.Value, ct);
        return true;
    }

}
