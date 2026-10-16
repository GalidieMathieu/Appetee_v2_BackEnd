using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Helpers;

public sealed class CatalogueOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation,OperationFilterContext context)
    {
        if (!context.ApiDescription.ActionDescriptor.RouteValues.TryGetValue("controller",out var controller) || controller!="Admin") return;
        foreach(var response in operation.Responses.Where(p=>p.Key.StartsWith('2')).Select(p=>p.Value))
            response.Headers["ETag"]=new OpenApiHeader { Description="Strong catalogue resource version, separate from Meal Plan revision.", Schema=new OpenApiSchema { Type="string" } };
        operation.Responses.TryAdd("401",new OpenApiResponse { Description="Authentication required or security version revoked." });
        operation.Responses.TryAdd("403",new OpenApiResponse { Description="Current server-side catalogue permission is required." });
        if(context.ApiDescription.HttpMethod=="PUT")
        {
            operation.Parameters.Add(new OpenApiParameter { Name="If-Match",In=ParameterLocation.Header,Required=true,
                Description="Exact strong ETag from this resource's admin read. Wildcard/weak/multiple tags are rejected.",Schema=new OpenApiSchema { Type="string" } });
            operation.Responses.TryAdd("428",new OpenApiResponse { Description="catalog_precondition_required: missing If-Match." });
            operation.Responses.TryAdd("412",new OpenApiResponse { Description="catalog_precondition_failed: no changes saved." });
        }
    }
}
