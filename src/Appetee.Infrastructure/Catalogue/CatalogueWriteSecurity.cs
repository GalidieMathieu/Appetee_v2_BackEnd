using System.Data;
using Appetee.Application.Abstractions.Catalogue;
using Appetee.Application.Models.Catalogue;
using Appetee.Application.utils;
using Dapper;

namespace Appetee.Infrastructure.Catalogue;

internal static class CatalogueWriteSecurity
{
    public static async Task LockActorAsync(IDbConnection connection, IDbTransaction transaction,
        ICatalogueActorContext? actor, CancellationToken ct)
    {
        if (actor is null || actor.UserId<=0) throw new ForbiddenException("Catalogue write permission is required.");
        var state = await connection.QuerySingleOrDefaultAsync<AccountSecurityState>(new CommandDefinition(
            "SELECT is_active AS IsActive, security_version AS SecurityVersion FROM users WHERE id=@UserId FOR UPDATE;",
            new { actor.UserId }, transaction, commandTimeout:15, cancellationToken:ct));
        if (state is null || !state.IsActive || state.SecurityVersion!=actor.SecurityVersion)
            throw new UnauthorizedException();
        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM account_permissions WHERE user_id=@UserId AND permission=@Permission);",
            new { actor.UserId, Permission=CataloguePermissions.Write }, transaction, commandTimeout:15, cancellationToken:ct));
        if (!allowed) throw new ForbiddenException("Catalogue write permission is required.");
    }

    public static async Task<CatalogueMetadata?> ReadMetadataAsync(IDbConnection connection, IDbTransaction? transaction,
        string resourceType, int id, bool forUpdate, CancellationToken ct)
    {
        var table = resourceType switch { "recipe"=>"recipes", "ingredient"=>"ingredients", _=>throw new ArgumentOutOfRangeException(nameof(resourceType)) };
        return await connection.QuerySingleOrDefaultAsync<CatalogueMetadata>(new CommandDefinition(
            $"SELECT resource_version AS ResourceVersion, created_by_user_id AS CreatedByUserId, updated_by_user_id AS UpdatedByUserId FROM {table} WHERE id=@Id"+(forUpdate ? " FOR UPDATE;" : ";"),
            new { Id=id }, transaction, commandTimeout:15, cancellationToken:ct));
    }

    public static void RequireVersion(CatalogueMetadata resource, ICatalogueActorContext? actor)
    {
        if (actor?.ExpectedResourceVersion is null) throw new CataloguePreconditionException(true);
        if (resource.ResourceVersion!=actor.ExpectedResourceVersion) throw new CataloguePreconditionException(false);
        if (resource.ResourceVersion==long.MaxValue) throw new InternalServerException();
    }

    public static async Task<CatalogueMetadata> RecordAsync(IDbConnection connection, IDbTransaction transaction,
        ICatalogueActorContext actor, string type, int id, bool created, CancellationToken ct)
    {
        var table = type switch { "recipe"=>"recipes", "ingredient"=>"ingredients", _=>throw new ArgumentOutOfRangeException(nameof(type)) };
        var sql = created
            ? $"UPDATE {table} SET created_by_user_id=@UserId,updated_by_user_id=@UserId WHERE id=@Id;"
            : $"UPDATE {table} SET resource_version=resource_version+1,updated_by_user_id=@UserId WHERE id=@Id;";
        await connection.ExecuteAsync(new CommandDefinition(sql,new { actor.UserId, Id=id },transaction,commandTimeout:15,cancellationToken:ct));
        var metadata = (await ReadMetadataAsync(connection,transaction,type,id,false,ct))!;
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO administrative_audit(actor_user_id,actor_reference,operation,resource_type,resource_id,resource_version,permission,occurred_at,correlation_id)
            VALUES(@UserId,@ActorReference,@Operation,@Type,@Id,@ResourceVersion,NULL,@At,@CorrelationId);
            """,new { actor.UserId, ActorReference=$"account:{actor.UserId}", Operation=created ? "catalogue_created" : "catalogue_updated", Type=type, Id=id,
                metadata.ResourceVersion, At=DateTime.UtcNow, actor.CorrelationId },transaction,commandTimeout:15,cancellationToken:ct));
        return metadata;
    }
}
