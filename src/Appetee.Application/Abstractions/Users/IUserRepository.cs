using Appetee.Application.Requests;

namespace Appetee.Application.Abstractions.Users;

public interface IUserRepository
{
    Task UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct);

}
