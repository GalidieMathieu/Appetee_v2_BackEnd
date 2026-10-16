namespace Appetee.Application.Dtos;

public sealed record UserSessionDto(
    int userId,
    string Username
)
{
    /// <summary>Current server-side capabilities; UI hints, not authorization credentials.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}
