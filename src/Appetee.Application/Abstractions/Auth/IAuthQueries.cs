using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;

namespace Appetee.Application.Abstractions.Auth;

public interface IAuthQueries
{
   Task<AuthResult> LoginAsync(LoginRequest user, CancellationToken ct);
}