using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Text.RegularExpressions;

namespace Appetee.Api.Tests.Infrastructure;

internal sealed class ApiTestDatabase
{
    private const string TestDatabaseName = "appetee_backend_tests";
    private static readonly Regex UseDatabaseRegex = new(@"USE\s+appetee\s*;", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly Lazy<string> _solutionRoot;
    private readonly Lazy<string> _connectionString;

    public ApiTestDatabase()
    {
        _solutionRoot = new Lazy<string>(FindSolutionRoot);
        _connectionString = new Lazy<string>(BuildConnectionString);
    }

    public string ConnectionString => _connectionString.Value;

    public async Task ResetAsync(CancellationToken ct = default)
    {
        var adminConnectionString = new MySqlConnectionStringBuilder(ConnectionString)
        {
            Database = string.Empty,
            AllowUserVariables = true,
        }.ConnectionString;

        await using var connection = new MySqlConnection(adminConnectionString);
        await connection.OpenAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            $"CREATE DATABASE IF NOT EXISTS `{TestDatabaseName}`;",
            cancellationToken: ct));

        foreach (var scriptName in new[] { "scriptDatabase.sql", "initObjectDatabase.sql" })
        {
            var scriptPath = Path.Combine(_solutionRoot.Value, scriptName);
            var scriptText = await File.ReadAllTextAsync(scriptPath, ct);

            // Redirect the shared scripts to a dedicated test database so local developer data stays untouched.
            var rewrittenScript = UseDatabaseRegex.Replace(
                scriptText,
                $"CREATE DATABASE IF NOT EXISTS `{TestDatabaseName}`;{Environment.NewLine}USE `{TestDatabaseName}`;");

            await connection.ExecuteAsync(new CommandDefinition(
                rewrittenScript,
                cancellationToken: ct,
                commandTimeout: 120));
        }
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: ct));
    }

    private string BuildConnectionString()
    {
        var apiProjectPath = Path.Combine(_solutionRoot.Value, "src", "Appetee.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var rawConnectionString = configuration.GetConnectionString("AppeteeDb")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing Appetee API connection string configuration.");

        var builder = new MySqlConnectionStringBuilder(rawConnectionString)
        {
            Database = TestDatabaseName,
            AllowUserVariables = true,
            SslMode = MySqlSslMode.None,
        };

        if (builder.Port == 0)
        {
            builder.Port = 3306;
        }

        return builder.ConnectionString;
    }

    private string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Appetee.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the solution root for the Appetee tests.");
    }
}
