namespace Appetee.Application.Requests;

/// <summary>
/// Profile update request. Null values mean "do not change".
/// </summary>
public sealed record UpdateUserRequest(
    string? DisplayName,
    string? ImageUrl
);
