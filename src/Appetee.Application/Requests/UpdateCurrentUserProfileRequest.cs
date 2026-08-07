using System.Text.Json.Serialization;

namespace Appetee.Application.Requests;

/// <summary>
/// Claim-scoped profile update. Null values mean "do not change".
/// Unknown fields are rejected so identity and server-owned fields cannot be
/// silently overposted.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCurrentUserProfileRequest(
    string? Username,
    string? ImageUrl
);
