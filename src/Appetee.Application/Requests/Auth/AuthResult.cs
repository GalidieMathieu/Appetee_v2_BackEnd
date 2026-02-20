using Appetee.Application.Dtos;

namespace Appetee.Application.Models.Auth;

public sealed record AuthResult(
    int userId,
    string userName
);