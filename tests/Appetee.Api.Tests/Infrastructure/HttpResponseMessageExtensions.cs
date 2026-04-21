using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Infrastructure;

internal static class HttpResponseMessageExtensions
{
    public static async Task<ProblemDetails?> ReadProblemDetailsAsync(this HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProblemDetails>();
    }
}
