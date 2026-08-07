namespace Appetee.Application.Dtos;

/// <summary>
/// Editable profile fields for the authenticated account.
/// </summary>
public sealed record CurrentUserProfileDto(
    string Username,
    string? ImageUrl
);
