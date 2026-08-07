namespace Appetee.Application.Dtos;

/// <summary>
/// Public sign-up response indicating whether an email is already registered.
/// </summary>
public sealed record EmailExistsDto(bool Exists);
