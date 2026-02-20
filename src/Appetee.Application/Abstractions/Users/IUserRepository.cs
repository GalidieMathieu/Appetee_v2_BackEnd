using Appetee.Application.Requests;

namespace Appetee.Application.Abstractions.Users;

public interface IUserRepository
{
    Task<bool> UpdateProfileAsync(int id, UpdateUserRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
