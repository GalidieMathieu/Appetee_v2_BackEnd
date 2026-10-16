using Appetee.Application.Abstractions.Catalogue;
using Appetee.Infrastructure.Data;
using Appetee.Application.Models.Catalogue;
using Dapper;

namespace Appetee.Infrastructure.Catalogue;

public sealed class CatalogueAuthorizationQueries(IDbConnectionFactory db) : ICatalogueAuthorizationQueries
{
    public async Task<IReadOnlyList<string>?> GetPermissionsAsync(int userId, long securityVersion, CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        // A single statement distinguishes a valid account with no grants from a revoked session.
        var rows = (await connection.QueryAsync<string?>(new CommandDefinition("""
            SELECT p.permission FROM users u
            LEFT JOIN account_permissions p ON p.user_id=u.id AND p.permission IN (@ReadAdmin,@Write)
            WHERE u.id=@UserId AND u.is_active=1 AND u.security_version=@SecurityVersion
            ORDER BY p.permission;
            """, new { UserId=userId, SecurityVersion=securityVersion,
                ReadAdmin=CataloguePermissions.ReadAdmin, Write=CataloguePermissions.Write },
            commandTimeout:15, cancellationToken:ct))).AsList();
        return rows.Count == 0 ? null : rows.OfType<string>().ToArray();
    }

    public async Task<AccountSecurityState?> GetAccountSecurityAsync(int userId, CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AccountSecurityState>(new CommandDefinition(
            "SELECT is_active AS IsActive, security_version AS SecurityVersion FROM users WHERE id=@UserId;",
            new { UserId = userId }, commandTimeout: 15, cancellationToken: ct));
    }

    public async Task<bool> HasPermissionAsync(int userId, long securityVersion, string permission, CancellationToken ct)
    {
        using var connection = await db.CreateOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT EXISTS(SELECT 1 FROM users u JOIN account_permissions p ON p.user_id=u.id
            WHERE u.id=@UserId AND u.is_active=1 AND u.security_version=@SecurityVersion AND p.permission=@Permission);
            """, new { UserId=userId, SecurityVersion=securityVersion, Permission=permission }, commandTimeout:15, cancellationToken:ct));
    }
}
