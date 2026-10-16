using Appetee.Application.utils;

namespace Appetee.Application.Models.Catalogue;

public static class CataloguePermissions
{
    public const string ReadAdmin = "Catalog.ReadAdmin";
    public const string Write = "Catalog.Write";
    public const string SecurityVersionClaim = "appetee:security-version";
}

public sealed record CatalogueMetadata(long ResourceVersion, int? CreatedByUserId, int? UpdatedByUserId);

public sealed record CatalogueResourceDto<T>(T Resource, string ResourceVersion, int? CreatedByUserId, int? UpdatedByUserId);

public sealed class CataloguePreconditionException : ApiException
{
    public CataloguePreconditionException(bool missing)
        : base(missing ? 428 : 412, missing ? "An If-Match precondition is required." : "The catalogue precondition does not match.",
            code: missing ? "catalog_precondition_required" : "catalog_precondition_failed") { }
}
