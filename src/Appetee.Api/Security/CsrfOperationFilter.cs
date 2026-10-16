using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Security;

internal sealed class CsrfOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!BrowserCsrfMiddleware.IsUnsafe(context.ApiDescription.HttpMethod ?? string.Empty))
            return;
        operation.Parameters ??= new List<OpenApiParameter>();
        foreach (var name in new[] { BrowserCsrfMiddleware.HeaderName, "Origin" })
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = name,
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema { Type = "string" },
                Description = name == "Origin"
                    ? "Trusted frontend origin (automatically supplied by browsers)."
                    : "Token from GET /api/auth/csrf with its matching cookie. Refresh after identity changes.",
            });
        }
        operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Invalid request or csrf_invalid; rejected before mutation." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Origin or browser context is not allowed." });
    }
}
