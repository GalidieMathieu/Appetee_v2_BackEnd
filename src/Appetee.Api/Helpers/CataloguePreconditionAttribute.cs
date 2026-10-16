using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

namespace Appetee.Api.Helpers;

/// <summary>Check header syntax after authentication/CSRF but before multipart body binding.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CataloguePreconditionAttribute(string resourceType) : Attribute,IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context,ResourceExecutionDelegate next)
    {
        var id=int.Parse(context.RouteData.Values["id"]!.ToString()!,CultureInfo.InvariantCulture);
        CatalogueEtags.Require(context.HttpContext,resourceType,id);
        await next();
    }
}
