namespace Appetee.Application.Requests.Auth;

public sealed record SignUpRequest(
    string Username,
    string DisplayName,
    string Email,
    string Password,
    IReadOnlyList<int>? DietIds = null,
    IReadOnlyList<int>? IngredientRestrictionIds = null
);