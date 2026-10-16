using Appetee.Application.utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Appetee.Api.Helpers;

/// <summary>Keep admin failure logging bounded and sanitize SQL details, including development responses.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CatalogueBoundaryAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var executed=await next();
        var status=(executed.Result as ObjectResult)?.StatusCode ?? context.HttpContext.Response.StatusCode;
        if(executed.Exception is {} exception && !executed.ExceptionHandled)
        {
            status=exception is ApiException api ? api.StatusCode : 500;
            if(exception is not (ApiException or OperationCanceledException)) executed.Exception=new InternalServerException(inner: exception);
        }
        context.HttpContext.RequestServices.GetRequiredService<ILogger<CatalogueBoundaryAttribute>>().LogInformation(
            Appetee.Application.Diagnostics.LogEvents.CatalogueOperation,
            "Catalogue request completed. Operation {Operation}; StatusCode {StatusCode}; TraceId {TraceId}",
            context.ActionDescriptor.RouteValues["action"],status,context.HttpContext.TraceIdentifier);
    }
}
