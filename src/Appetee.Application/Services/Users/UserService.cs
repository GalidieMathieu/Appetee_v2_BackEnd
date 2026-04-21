using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.utils;
using System.Security.Claims;

namespace Appetee.Application.Services.Users;

public sealed class UserService : IUserService
{
    private readonly IUserQueries _queries;
    private readonly IUserRepository _repo;

    public UserService(IUserQueries queries, IUserRepository repo)
    {
        _queries = queries;
        _repo = repo;
    }

    public Task<UserDto?> GetByIdAsync(int id, CancellationToken ct)
        => _queries.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<UserDto>> ListAsync(int skip, int take, CancellationToken ct)
    => _queries.ListAsync(skip, take, ct);


    //Check If the email exist
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct) => _queries.checkExistByEmailAsync(email, ct);





    //TODO To implement
    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        if (request.Username is not null)
        {
            var username = request.Username.Trim();
            if (string.IsNullOrWhiteSpace(username))
                throw new ValidationException("Username is required.");
            if (username.Length > 50)
                throw new ValidationException("Username too long (max 50).");

            request = request with { Username = username };
        }

        if (request.ImageUrl is not null && request.ImageUrl.Length > 255)
            throw new ValidationException("ImageUrl too long (max 255).");

        var updated = await _repo.UpdateProfileAsync(id, request, ct);
        if (!updated) return null;

        return await _queries.GetByIdAsync(id, ct);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct)
        => _repo.DeleteAsync(id, ct);
}
