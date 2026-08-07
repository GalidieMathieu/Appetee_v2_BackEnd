using Appetee.Application.Abstractions.Users;
using Appetee.Application.Requests;
using Appetee.Infrastructure.Data;
using Dapper;

namespace Appetee.Infrastructure.Users;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _db;

    public UserRepository(IDbConnectionFactory db) => _db = db;

    public async Task UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct)
    {
        using var connection = await _db.CreateOpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                UserSql.UpdateCurrentProfile,
                new
                {
                    currentUserId,
                    username = request.Username,
                    imageUrl = request.ImageUrl
                },
                cancellationToken: ct));
    }
}
