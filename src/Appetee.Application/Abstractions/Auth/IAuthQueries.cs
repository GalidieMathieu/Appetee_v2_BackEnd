using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;

namespace Appetee.Application.Abstractions.Auth;

public interface IAuthQueries
{
    Task<LoginAttempt> LoginAsync(LoginRequest user, CancellationToken ct);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);

}
