using Appetee.Application.Dtos;
using Appetee.Infrastructure.Data;
using Appetee.Application.Abstractions.Users;
using Dapper;

namespace Appetee.Infrastructure.Users;

public sealed class UserQueries : IUserQueries
{
    private readonly IDbConnectionFactory _db;

    public UserQueries(IDbConnectionFactory db) => _db = db;

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        return await conn.QuerySingleOrDefaultAsync<UserDto>(
            new CommandDefinition(UserSql.GetById, new { id }, cancellationToken: ct)
        );
    }

    public async Task<bool> checkExistByEmailAsync(string email, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<bool>(
            new CommandDefinition(UserSql.CheckExistByEmail, new { email }, cancellationToken: ct)
        );
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var rows = await conn.QueryAsync<UserDto>(
            new CommandDefinition(UserSql.List, new { skip, take }, cancellationToken: ct)
        );

        return rows.AsList();
    }
}
