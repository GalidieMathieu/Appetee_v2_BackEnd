namespace Appetee.Api.Security;

/// <summary>Explicit frontend trust shared with credentialed CORS; no wildcards.</summary>
internal sealed class BrowserRequestOriginPolicy
{
    private readonly HashSet<string> _origins = new(StringComparer.OrdinalIgnoreCase);

    public BrowserRequestOriginPolicy(IEnumerable<string> origins)
    {
        foreach (var origin in origins)
        {
            if (!TryNormalize(origin, out var normalized))
                throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit HTTP(S) origins, not wildcards or URLs with paths.");
            _origins.Add(normalized);
        }
    }

    internal bool IsAllowed(HttpRequest request)
    {
        var origins = request.Headers.Origin;
        if (origins.Count != 1 || !TryNormalize(origins[0], out var origin))
            return false;
        return _origins.Contains(origin)
            || (TryNormalize($"{request.Scheme}://{request.Host}", out var sameOrigin)
                && string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryNormalize(string? value, out string origin)
    {
        origin = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/"
            || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || value.Contains('*') || value.Contains(',') || value.Contains('\\'))
            return false;
        origin = uri.GetLeftPart(UriPartial.Authority);
        return string.Equals(value, origin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, origin + "/", StringComparison.OrdinalIgnoreCase);
    }
}
