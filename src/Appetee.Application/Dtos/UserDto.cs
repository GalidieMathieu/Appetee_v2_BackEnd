namespace Appetee.Application.Dtos;

public sealed record UserDto(
    int id,
    string username,
    string email,
    IReadOnlyList<int>? dietIds = null,
    IReadOnlyList<int>? ingredientRestrictionIds = null
);