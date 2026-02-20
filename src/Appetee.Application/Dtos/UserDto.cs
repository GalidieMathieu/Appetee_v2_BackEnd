namespace Appetee.Application.Dtos;

public sealed record UserDto(
    int Id,
    string Username,
    string Email,
    IReadOnlyList<int>? DietIds = null,
    IReadOnlyList<int>? IngredientRestrictionIds = null
);