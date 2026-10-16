using System.Globalization;
using Appetee.Api.Security;
using Appetee.Application.Models.Catalogue;

namespace Appetee.Api.Helpers;

internal static class CatalogueEtags
{
    internal static string Format(string type, int id, long version) => $"\"catalog-{type}-{id}-v{version}\"";

    internal static void Require(HttpContext context, string type, int id)
    {
        if (!context.Request.Headers.TryGetValue("If-Match",out var values) || string.IsNullOrWhiteSpace(values.ToString()))
            throw new CataloguePreconditionException(true);
        var token=values.ToString();
        var prefix=$"\"catalog-{type}-{id}-v";
        if (values.Count!=1 || !token.StartsWith(prefix,StringComparison.Ordinal) || !token.EndsWith('"') ||
            !long.TryParse(token.AsSpan(prefix.Length,token.Length-prefix.Length-1),NumberStyles.None,CultureInfo.InvariantCulture,out var version) ||
            version<=0 || token!=Format(type,id,version)) throw new CataloguePreconditionException(false);
        context.Items[typeof(CatalogueActorContext)]=version;
    }

    internal static CatalogueResourceDto<T> Result<T>(HttpResponse response,string type,int id,T resource,CatalogueMetadata metadata)
    {
        response.Headers.ETag=Format(type,id,metadata.ResourceVersion);
        response.Headers.CacheControl="no-store";
        return new(resource,metadata.ResourceVersion.ToString(CultureInfo.InvariantCulture),metadata.CreatedByUserId,metadata.UpdatedByUserId);
    }
}
