using Appetee.Application.Dtos;
using Appetee.Application.Requests;

namespace Appetee.Application.Services.Users;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);

    Task<IReadOnlyList<UserDto>> ListAsync(int skip, int take, CancellationToken ct);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
