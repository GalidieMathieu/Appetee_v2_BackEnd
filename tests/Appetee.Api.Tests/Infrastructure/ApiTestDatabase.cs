using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Text.RegularExpressions;

namespace Appetee.Api.Tests.Infrastructure;

internal sealed class ApiTestDatabase
{
    private const string TestDatabaseName = "appetee_backend_tests";
    private const string IntegrationOptInVariable = "APPETEE_RUN_DB_INTEGRATION_TESTS";
    private static readonly Regex UseDatabaseRegex = new(@"USE\s+appetee\s*;", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly Lazy<string> _solutionRoot;
    private readonly Lazy<string> _connectionString;

    public ApiTestDatabase()
    {
        _solutionRoot = new Lazy<string>(FindSolutionRoot);
        _connectionString = new Lazy<string>(BuildConnectionString);
    }

    public string ConnectionString => _connectionString.Value;

    internal Task<DatabaseEnvironment?> ReadEnvironmentAsync(CancellationToken ct=default)=>
        QuerySingleOrDefaultAsync<DatabaseEnvironment>("SELECT DATABASE() DatabaseName,VERSION() Version,@@sql_mode SqlMode,@@max_allowed_packet MaxAllowedPacket;",ct:ct);

    internal sealed record DatabaseEnvironment(string DatabaseName,string Version,string SqlMode,long MaxAllowedPacket)
    {public DatabaseEnvironment():this("","","",0){} }

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
        await connection.ExecuteAsync(new CommandDefinition(
            $"DROP TABLE IF EXISTS `{TestDatabaseName}`.`appetee_schema_migrations`;",
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

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<T>(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: ct));
        return rows.AsList();
    }

    public async Task ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: ct));
    }

    public async Task ExecuteMigrationAsync(string relativePath, CancellationToken ct = default)
    {
        var migrationPath = Path.Combine(
            _solutionRoot.Value,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var script = await File.ReadAllTextAsync(migrationPath, ct);

        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(ct);

        foreach (var commandText in Appetee.DatabaseMigrator.SqlScriptParser.Split(script))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                commandText,
                cancellationToken: ct,
                commandTimeout: 120));
        }
    }

    // Uses the same explicit opt-in, loopback-only connection and fixed disposable database as ResetAsync.
    // Never redirects an arbitrary target or weakens the dataset CLI's separate local-appetee reset guard.
    internal async Task ImportGeneratedDatasetAsync(CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        foreach (var file in new[] { "01-schema.sql", "02-reference.sql", "03-ingredients.sql", "04-recipes.sql" })
        {
            var script = await File.ReadAllTextAsync(Path.Combine(_solutionRoot.Value, "data", "generated", "sql", file), ct);
            var redirected = UseDatabaseRegex.Replace(script, $"USE `{TestDatabaseName}`;");
            await connection.ExecuteAsync(new CommandDefinition(redirected, cancellationToken: ct, commandTimeout: 180));
        }
    }

    private string BuildConnectionString()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(IntegrationOptInVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database integration tests require explicit opt-in with {IntegrationOptInVariable}=true.");
        }

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

        if (!IsLoopbackHost(builder.Server))
        {
            throw new InvalidOperationException(
                $"Database integration tests require a loopback MySQL host and the isolated '{TestDatabaseName}' database.");
        }

        if (builder.Port == 0)
        {
            builder.Port = 3306;
        }

        return builder.ConnectionString;
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
        || string.Equals(host, "::1", StringComparison.Ordinal);

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
