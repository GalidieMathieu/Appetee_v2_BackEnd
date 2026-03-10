namespace Appetee.Application.Dtos;

public sealed record UserSessionDto(
    int userId,
    string Username
);