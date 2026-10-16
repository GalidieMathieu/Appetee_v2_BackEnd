using Appetee.Application.utils;
using Microsoft.AspNetCore.Antiforgery;

namespace Appetee.Api.Security;

/// <summary>Rejects invalid browser intent before unsafe API actions or body binding.</summary>
internal sealed class BrowserCsrfMiddleware
{
    internal const string HeaderName = "X-CSRF-TOKEN";
    internal const string CookieName = "__Host-appetee-csrf";
    private readonly RequestDelegate _next;
    private readonly BrowserRequestOriginPolicy _origins;

    public BrowserCsrfMiddleware(RequestDelegate next, BrowserRequestOriginPolicy origins)
    {
        _next = next;
        _origins = origins;
    }

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }
        var unsafeMethod = IsUnsafe(context.Request.Method);
        var bootstrap = string.Equals(context.Request.Path.Value?.TrimEnd('/'),
                "/api/auth/csrf", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method);
        if (unsafeMethod || bootstrap)
        {
            // Only the safe bootstrap may omit Origin (for non-browser token acquisition).
            if ((unsafeMethod || context.Request.Headers.ContainsKey("Origin"))
                && !_origins.IsAllowed(context.Request))
                throw new ForbiddenException("Request origin is not allowed.");
            if (!HasSafeFetchMetadata(context.Request))
                throw new ForbiddenException("Browser request context is not allowed.");
        }
        if (unsafeMethod)
        {
            var token = context.Request.Headers[HeaderName];
            if (token.Count != 1 || string.IsNullOrWhiteSpace(token[0]))
                throw InvalidToken();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                // Never expose token/identity values or framework validation details.
                throw InvalidToken();
            }
        }
        await _next(context);
    }

    internal static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static ValidationException InvalidToken() =>
        new("The CSRF token is missing or invalid.", "csrf_invalid");

    private static bool HasSafeFetchMetadata(HttpRequest request)
    {
        // Metadata can be absent in old browsers; origin and token still remain mandatory.
        foreach (var (name, allowed) in new[]
        {
            ("Sec-Fetch-Site", new[] { "same-origin", "same-site", "cross-site" }),
            ("Sec-Fetch-Mode", new[] { "cors", "same-origin" }),
            ("Sec-Fetch-Dest", new[] { "empty" }),
        })
        {
            if (request.Headers.TryGetValue(name, out var value)
                && (value.Count != 1 || !allowed.Contains(value[0], StringComparer.Ordinal)))
                return false;
        }
        // A trusted cross-site SPA fetch is allowed, not an untrusted cross-site form.
        return true;
    }
}
