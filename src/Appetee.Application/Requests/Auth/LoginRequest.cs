namespace Appetee.Application.Requests.Auth;

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);

public sealed record LoginRow(
    int Id, 
    string Username,
    string PasswordHash
);
