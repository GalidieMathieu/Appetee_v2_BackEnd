using System.Globalization;
using System.Security.Claims;
using Appetee.Application.Abstractions.Catalogue;
using Appetee.Application.Models.Catalogue;
using Microsoft.AspNetCore.Authorization;

namespace Appetee.Api.Security;

internal sealed record CataloguePermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class CataloguePermissionHandler(ICatalogueAuthorizationQueries queries, IHttpContextAccessor accessor)
    : AuthorizationHandler<CataloguePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CataloguePermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated!=true) return;
        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id) || id<=0) return;
        if (!long.TryParse(context.User.FindFirstValue(CataloguePermissions.SecurityVersionClaim), NumberStyles.None,
            CultureInfo.InvariantCulture,out var version) || version<0) return;
        if (await queries.HasPermissionAsync(id,version,requirement.Permission,accessor.HttpContext?.RequestAborted ?? default))
            context.Succeed(requirement);
    }
}

internal sealed class CatalogueActorContext(IHttpContextAccessor accessor) : ICatalogueActorContext
{
    public int UserId => int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id) ? id : 0;
    public long SecurityVersion => long.TryParse(accessor.HttpContext?.User.FindFirstValue(CataloguePermissions.SecurityVersionClaim),out var value) ? value : -1;
    public long? ExpectedResourceVersion => accessor.HttpContext?.Items[typeof(CatalogueActorContext)] as long?;
    public string CorrelationId => accessor.HttpContext?.TraceIdentifier ?? "unavailable";
}
