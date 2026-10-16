using System.Data;
using System.Globalization;
using MySqlConnector;

namespace Appetee.DatabaseMigrator;

/// <summary>Trusted operator workflow using the migration identity, never an HTTP/self-service API.</summary>
public sealed class PermissionOperator(string connectionString)
{
    public async Task<bool> ChangeAsync(int targetUserId, string permission, bool grant, string operatorReference,
        int? actorUserId, CancellationToken ct)
    {
        if (targetUserId<=0 || actorUserId is <=0 || permission is not ("Catalog.ReadAdmin" or "Catalog.Write"))
            throw new InvalidOperationException("A positive target ID and an approved permission are required.");
        if (string.IsNullOrWhiteSpace(operatorReference) || operatorReference.Length>120 || operatorReference.Any(char.IsControl))
            throw new InvalidOperationException("An explicit plain-text operator reference of 1-120 characters is required.");
        await using var connection=new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        try
        {
            await using var user=new MySqlCommand("SELECT security_version FROM users WHERE id=@Id FOR UPDATE;",connection,transaction);
            user.CommandTimeout=15;
            user.Parameters.AddWithValue("Id",targetUserId);
            var rawVersion=await user.ExecuteScalarAsync(ct);
            if (rawVersion is null) throw new InvalidOperationException("Target account is unavailable.");
            var version=Convert.ToInt64(rawVersion,CultureInfo.InvariantCulture);
            if (version==long.MaxValue) throw new InvalidOperationException("Security version is exhausted.");
            await using var lookup=new MySqlCommand("SELECT COUNT(*) FROM account_permissions WHERE user_id=@Id AND permission=@Permission;",connection,transaction);
            lookup.CommandTimeout=15;
            lookup.Parameters.AddWithValue("Id",targetUserId);
            lookup.Parameters.AddWithValue("Permission",permission);
            var exists=Convert.ToInt32(await lookup.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0;
            if (exists==grant) { await transaction.CommitAsync(ct); return false; }
            await using var change=new MySqlCommand(grant
                ? "INSERT INTO account_permissions(user_id,permission,granted_at,granted_by_user_id) VALUES(@Id,@Permission,@At,@Actor);"
                : "DELETE FROM account_permissions WHERE user_id=@Id AND permission=@Permission;",connection,transaction);
            var timestamp=DateTime.UtcNow;
            change.CommandTimeout=15;
            change.Parameters.AddWithValue("Id",targetUserId);
            change.Parameters.AddWithValue("Permission",permission);
            change.Parameters.AddWithValue("At",timestamp);
            change.Parameters.AddWithValue("Actor",actorUserId is {} actor ? actor : DBNull.Value);
            await change.ExecuteNonQueryAsync(ct);
            await using var audit=new MySqlCommand("""
                UPDATE users SET security_version=security_version+1 WHERE id=@Id;
                INSERT INTO administrative_audit(actor_user_id,actor_reference,operation,resource_type,resource_id,resource_version,permission,occurred_at,correlation_id)
                VALUES(@Actor,@Operator,@Operation,'account',@Id,NULL,@Permission,@At,@Correlation);
                """,connection,transaction);
            audit.CommandTimeout=15;
            audit.Parameters.AddWithValue("Id",targetUserId);
            audit.Parameters.AddWithValue("Actor",actorUserId is {} actorId ? actorId : DBNull.Value);
            audit.Parameters.AddWithValue("Operator",operatorReference.Trim());
            audit.Parameters.AddWithValue("Operation",grant ? "permission_granted" : "permission_revoked");
            audit.Parameters.AddWithValue("Permission",permission);
            audit.Parameters.AddWithValue("At",timestamp);
            audit.Parameters.AddWithValue("Correlation",Guid.NewGuid().ToString("N"));
            await audit.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
