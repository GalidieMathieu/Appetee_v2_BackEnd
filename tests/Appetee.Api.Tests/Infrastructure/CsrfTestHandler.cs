using System.Net.Http.Json;

namespace Appetee.Api.Tests.Infrastructure;

/// <summary>Exercises real bootstrap/validation for ordinary tests; never retries a mutation.</summary>
internal sealed class CsrfTestHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && request.Method.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            // Test-only convenience: acquire under the current cookie identity before each intent.
            using var bootstrap = new HttpRequestMessage(HttpMethod.Get,
                new Uri(request.RequestUri, "/api/auth/csrf"));
            using var response = await base.SendAsync(bootstrap, cancellationToken);
            response.EnsureSuccessStatusCode();
            var tokens = await response.Content.ReadFromJsonAsync<Token>(cancellationToken)
                ?? throw new InvalidOperationException("Missing CSRF bootstrap response.");
            request.Headers.TryAddWithoutValidation(tokens.HeaderName, tokens.RequestToken);
            request.Headers.TryAddWithoutValidation("Origin", "https://localhost:4200");
        }
        return await base.SendAsync(request, cancellationToken);
    }

    private sealed record Token(string RequestToken, string HeaderName);
}
