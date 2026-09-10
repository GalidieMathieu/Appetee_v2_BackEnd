using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.RowData;
using Appetee.Infrastructure.Data;
using Dapper;

namespace Appetee.Infrastructure.Users;

public sealed class UserQueries : IUserQueries
{
    private readonly IDbConnectionFactory _db;

    public UserQueries(IDbConnectionFactory db) => _db = db;

    public async Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<CurrentUserProfileRow>(
            new CommandDefinition(
                UserSql.GetCurrentProfile,
                new { currentUserId },
                cancellationToken: ct));

        return row is null
            ? null
            : new CurrentUserProfileDto(row.Username, row.ImageUrl);
    }
}
