using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.Abstractions.Users;
using System.Reflection.Metadata.Ecma335;

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

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        // Minimal validation (best-practice; adjust to your needs)
        if (request.DisplayName is not null && request.DisplayName.Length > 100)
            throw new ArgumentException("DisplayName too long (max 100).", nameof(request.DisplayName));

        if (request.ImageUrl is not null && request.ImageUrl.Length > 255)
            throw new ArgumentException("ImageUrl too long (max 255).", nameof(request.ImageUrl));

        var updated = await _repo.UpdateProfileAsync(id, request, ct);
        if (!updated) return null;

        return await _queries.GetByIdAsync(id, ct);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct)
        => _repo.DeleteAsync(id, ct);
}
