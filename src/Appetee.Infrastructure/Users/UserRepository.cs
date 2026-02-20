using Appetee.Application.Requests;
using Appetee.Infrastructure.Data;
using Appetee.Application.Abstractions.Users;
using Dapper;

namespace Appetee.Infrastructure.Users;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _db;

    public UserRepository(IDbConnectionFactory db) => _db = db;

    public async Task<bool> UpdateProfileAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var affected = await conn.ExecuteAsync(
            new CommandDefinition(
                UserSql.UpdateProfile,
                new { id, displayName = request.DisplayName, imageUrl = request.ImageUrl },
                cancellationToken: ct
            )
        );

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var affected = await conn.ExecuteAsync(
            new CommandDefinition(UserSql.DeleteById, new { id }, cancellationToken: ct)
        );

        return affected > 0;
    }
}
