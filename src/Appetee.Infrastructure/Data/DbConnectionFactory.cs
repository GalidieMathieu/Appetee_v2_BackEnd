using System.Data;
using MySqlConnector;
using Appetee.Application.utils;

namespace Appetee.Infrastructure.Data;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ValidationException("Connection string is required.");

        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
