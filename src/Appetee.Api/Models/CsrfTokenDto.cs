namespace Appetee.Api.Models;

/// <summary>Request-intent token, not a login credential. Keep it in client memory.</summary>
public sealed record CsrfTokenDto(string RequestToken, string HeaderName);
