using Appetee.Application.Dtos;
using Appetee.Application.Requests;

namespace Appetee.Application.Services.Users;

public interface IUserService
{
    Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct);

    Task<CurrentUserProfileDto?> UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct);

}
