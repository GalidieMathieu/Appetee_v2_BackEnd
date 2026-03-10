using Appetee.Application.Dtos;
using Appetee.Infrastructure.Data;
using Appetee.Application.Abstractions.Users;
using Dapper;
using Appetee.Application.RowData;

namespace Appetee.Infrastructure.Users;

public sealed class UserQueries : IUserQueries
{
    private readonly IDbConnectionFactory _db;

    public UserQueries(IDbConnectionFactory db) => _db = db;

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        using var grid = await conn.QueryMultipleAsync(
       new CommandDefinition(UserSql.GetUserWithPreferencesById, new { id }, cancellationToken: ct)
        );

        // 1) user row
        var userBase = await grid.ReadSingleOrDefaultAsync<UserBaseRow>();
        if (userBase is null) return null;

        var dietIds = (await grid.ReadAsync<int>()).AsList();
        var ingredientIds = (await grid.ReadAsync<int>()).AsList();

        return new UserDto(
           id: userBase.Id,
           username: userBase.Username,
           email: userBase.Email,
           dietIds: dietIds.Count == 0 ? null : dietIds,
           ingredientRestrictionIds: ingredientIds.Count == 0 ? null : ingredientIds
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
