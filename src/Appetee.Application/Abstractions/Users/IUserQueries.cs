using Appetee.Application.Dtos;

namespace Appetee.Application.Abstractions.Users;

public interface IUserQueries
{
    Task<UserDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> checkExistByEmailAsync(string email, CancellationToken ct);

    Task<IReadOnlyList<UserDto>> ListAsync(int skip, int take, CancellationToken ct);
}
