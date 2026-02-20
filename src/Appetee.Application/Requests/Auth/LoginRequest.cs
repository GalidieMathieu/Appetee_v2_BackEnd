namespace Appetee.Application.Requests.Auth;

public sealed record LoginRequest(
    string Identifier, // username OR email
    string Password
);