using Appetee.Application.Dtos;

namespace Appetee.Application.Abstractions.Users;

public interface IUserQueries
{
    Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct);
}
