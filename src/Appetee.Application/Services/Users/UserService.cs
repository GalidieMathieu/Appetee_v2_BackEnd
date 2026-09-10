using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Application.Services.Users;

public sealed class UserService : IUserService
{
    private readonly IUserQueries _queries;
    private readonly IUserRepository _repository;

    public UserService(IUserQueries queries, IUserRepository repository)
    {
        _queries = queries;
        _repository = repository;
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

}
