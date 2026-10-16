using MySqlConnector;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Appetee.DatabaseMigrator;

public sealed class MigrationRunner
{
    private const string HistoryTable = "appetee_schema_migrations";
    private const string BaselineOptInVariable = "APPETEE_ALLOW_MIGRATION_BASELINE";
    private static readonly Regex MigrationNamePattern = new(
        @"^(?<date>\d{8})_(?<sequence>\d{3})_[a-z0-9_]+\.sql$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _connectionString;
    private readonly TextWriter _output;
    private readonly IReadOnlyList<MigrationDefinition> _migrations;
    private readonly string _baselineSchema;

    public MigrationRunner(string connectionString, TextWriter output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _output = output ?? throw new ArgumentNullException(nameof(output));

        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
            throw new InvalidOperationException("The migration connection string must name a database.");

        _connectionString = builder.ConnectionString;
        (_migrations, _baselineSchema) = LoadEmbeddedResources();
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await ValidateServerAsync(connection, ct);

        var lockName = $"appetee:migrations:{connection.Database}";
        await AcquireLockAsync(connection, lockName, ct);
        try
        {
            var existingTableCount = await CountApplicationTablesAsync(connection, ct);
            await EnsureHistoryTableAsync(connection, ct);
            var applied = await LoadHistoryAsync(connection, ct);

            if (existingTableCount == 0)
            {
                await ApplyCleanBaselineAsync(connection, ct);
                foreach (var migration in _migrations)
                    await RecordAsync(connection, migration, "baseline", 0, ct);

                await _output.WriteLineAsync(
                    $"Created clean schema and baselined {_migrations.Count} migrations.");
                return;
            }

            if (applied.Count == 0)
            {
                throw new InvalidOperationException(
                    "Existing schema has no migration history. Run 'baseline --through <sequence>' " +
                    $"with {BaselineOptInVariable}=true after verifying that schema version.");
            }

            VerifyAppliedChecksums(applied);
            foreach (var migration in _migrations.Where(item => !applied.ContainsKey(item.Id)))
                await ApplyMigrationAsync(connection, migration, ct);

            await _output.WriteLineAsync("Database migrations are current.");
        }
        finally
        {
            await ReleaseLockAsync(connection, lockName);
        }
    }

    public async Task BaselineExistingAsync(int throughSequence, CancellationToken ct = default)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(BaselineOptInVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Existing-schema baselining requires {BaselineOptInVariable}=true.");
        }

        var baselineMigrations = _migrations
            .Where(item => item.Sequence <= throughSequence)
            .ToArray();
        if (baselineMigrations.Length == 0
            || baselineMigrations[^1].Sequence != throughSequence)
        {
            throw new InvalidOperationException(
                $"Migration sequence {throughSequence:D3} does not exist in this build.");
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await ValidateServerAsync(connection, ct);

        var lockName = $"appetee:migrations:{connection.Database}";
        await AcquireLockAsync(connection, lockName, ct);
        try
        {
            if (await CountApplicationTablesAsync(connection, ct) == 0)
                throw new InvalidOperationException("Cannot baseline an empty schema; use 'migrate'.");

            await EnsureHistoryTableAsync(connection, ct);
            if ((await LoadHistoryAsync(connection, ct)).Count != 0)
                throw new InvalidOperationException("Migration history is not empty; baselining is not allowed.");

            await ValidateExistingBaselineShapeAsync(connection, ct);
            foreach (var migration in baselineMigrations)
                await RecordAsync(connection, migration, "baseline", 0, ct);

            await _output.WriteLineAsync(
                $"Recorded existing schema through migration {throughSequence:D3}. Run 'migrate' to apply later migrations.");
        }
        finally
        {
            await ReleaseLockAsync(connection, lockName);
        }
    }

    /// <summary>
    /// Repairs only the missing legacy account-cleanup table. Does not adopt or
    /// forge history for an existing schema whose previously applied SQL is unknown.
    /// </summary>
    public async Task RepairAccountClosureAsync(CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await ValidateServerAsync(connection, ct);
        var lockName = $"appetee:migrations:{connection.Database}";
        await AcquireLockAsync(connection, lockName, ct);
        try
        {
            await using (var historyExists = new MySqlCommand("""
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_schema=DATABASE() AND table_name='appetee_schema_migrations';
                """, connection))
            {
                if (Convert.ToInt32(await historyExists.ExecuteScalarAsync(ct)) != 0
                    && (await LoadHistoryAsync(connection, ct)).Count != 0)
                    throw new InvalidOperationException("Legacy account-closure repair requires empty/absent migration history. A tracked database requires audited forward recovery.");
            }
            await using (var prerequisites = new MySqlCommand("""
                SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE()
                AND ((table_name='recipes' AND column_name IN ('description','card_image_blob_name','calories_per_serving','protein_per_serving'))
                  OR (table_name='recipe_ingredients' AND column_name IN ('display_order','featured_order')));
                """, connection))
            {
                if (Convert.ToInt32(await prerequisites.ExecuteScalarAsync(ct)) != 6)
                    throw new InvalidOperationException("Legacy schema prerequisites are absent; account-closure repair cannot run on this database.");
            }
            await using (var tableExists = new MySqlCommand("""
                SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()
                  AND table_name='account_closure_cleanup';
                """, connection))
            {
                if (Convert.ToInt32(await tableExists.ExecuteScalarAsync(ct)) != 0)
                {
                    await ValidateAccountClosureColumnsAsync(connection, ct);
                    await _output.WriteLineAsync("Account-closure cleanup table already exists; no schema or history was changed.");
                    return;
                }
            }
            var migration = _migrations.Single(item => item.Sequence == 7);
            // Execute the immutable historical definition, but do NOT record a lone
            // 007 entry: doing so would make migrate replay earlier, possibly applied SQL.
            foreach (var statement in SqlScriptParser.Split(migration.Sql))
                await ExecuteAsync(connection, statement, ct);
            await ValidateAccountClosureColumnsAsync(connection, ct);
            await _output.WriteLineAsync("Created account_closure_cleanup from migration 007. Existing data and migration history were preserved; schema adoption remains a separate audited step.");
        }
        finally { await ReleaseLockAsync(connection, lockName); }
    }

    private static async Task ValidateAccountClosureColumnsAsync(MySqlConnection connection, CancellationToken ct)
    {
        await using var command = new MySqlCommand("""
            SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE()
              AND table_name='account_closure_cleanup' AND column_name IN
              ('id','former_user_id','profile_image_url','status','attempt_count','next_attempt_at',
               'last_error_code','created_at','updated_at','completed_at');
            """, connection);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 10)
            throw new InvalidOperationException("The existing account-closure table has an unexpected shape; repair will not overwrite it.");
    }

    private async Task ApplyCleanBaselineAsync(MySqlConnection connection, CancellationToken ct)
    {
        var createStatements = SqlScriptParser.Split(_baselineSchema)
            .Where(statement => StripLeadingComments(statement)
                .StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (createStatements.Length == 0)
            throw new InvalidOperationException("The embedded canonical baseline contains no tables.");

        foreach (var statement in createStatements)
            await ExecuteAsync(connection, statement, ct);
    }

    private async Task ApplyMigrationAsync(
        MySqlConnection connection,
        MigrationDefinition migration,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        foreach (var statement in SqlScriptParser.Split(migration.Sql))
            await ExecuteAsync(connection, statement, ct);

        stopwatch.Stop();
        await RecordAsync(connection, migration, "migration", stopwatch.ElapsedMilliseconds, ct);
        await _output.WriteLineAsync($"Applied {migration.Id}.");
    }

    private static async Task ValidateServerAsync(MySqlConnection connection, CancellationToken ct)
    {
        await using var command = new MySqlCommand(
            "SELECT @@version, @@version_comment;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var versionText = reader.GetString(0);
        var comment = reader.GetString(1);
        await reader.CloseAsync();

        if (versionText.Contains("MariaDB", StringComparison.OrdinalIgnoreCase)
            || comment.Contains("MariaDB", StringComparison.OrdinalIgnoreCase)
            || !Version.TryParse(versionText.Split('-', 2)[0], out var version)
            || version < new Version(8, 0, 16))
        {
            throw new InvalidOperationException(
                $"Appetee migrations require MySQL 8.0.16 or later with enforced CHECK constraints; detected '{versionText}'.");
        }
    }

    private static async Task<int> CountApplicationTablesAsync(
        MySqlConnection connection,
        CancellationToken ct)
    {
        await using var command = new MySqlCommand(
            $"""
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_type = 'BASE TABLE'
              AND table_name <> '{HistoryTable}';
            """,
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task EnsureHistoryTableAsync(
        MySqlConnection connection,
        CancellationToken ct) =>
        await ExecuteAsync(
            connection,
            $"""
            CREATE TABLE IF NOT EXISTS {HistoryTable} (
                migration_id VARCHAR(255) NOT NULL PRIMARY KEY,
                sequence_number INT NOT NULL,
                checksum_sha256 CHAR(64) NOT NULL,
                execution_kind VARCHAR(16) NOT NULL,
                duration_ms BIGINT NOT NULL,
                applied_at_utc DATETIME(6) NOT NULL,
                applied_by VARCHAR(255) NOT NULL,
                UNIQUE KEY uq_schema_migrations_sequence (sequence_number),
                CONSTRAINT chk_schema_migrations_kind
                    CHECK (execution_kind IN ('baseline', 'migration')),
                CONSTRAINT chk_schema_migrations_duration
                    CHECK (duration_ms >= 0)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            """,
            ct);

    private static async Task<Dictionary<string, string>> LoadHistoryAsync(
        MySqlConnection connection,
        CancellationToken ct)
    {
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new MySqlCommand(
            $"SELECT migration_id, checksum_sha256 FROM {HistoryTable} ORDER BY sequence_number;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            output.Add(reader.GetString(0), reader.GetString(1));

        return output;
    }

    private void VerifyAppliedChecksums(IReadOnlyDictionary<string, string> applied)
    {
        foreach (var migration in _migrations)
        {
            if (applied.TryGetValue(migration.Id, out var checksum)
                && !string.Equals(checksum, migration.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Applied migration {migration.Id} no longer matches its immutable checksum.");
            }
        }

        var unknown = applied.Keys.Except(_migrations.Select(item => item.Id), StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
            throw new InvalidOperationException($"Migration history contains unknown migration {unknown}.");
    }

    private static async Task RecordAsync(
        MySqlConnection connection,
        MigrationDefinition migration,
        string executionKind,
        long durationMs,
        CancellationToken ct)
    {
        await using var command = new MySqlCommand(
            $"""
            INSERT INTO {HistoryTable} (
                migration_id,
                sequence_number,
                checksum_sha256,
                execution_kind,
                duration_ms,
                applied_at_utc,
                applied_by
            ) VALUES (
                @id,
                @sequence,
                @checksum,
                @kind,
                @duration,
                UTC_TIMESTAMP(6),
                CURRENT_USER()
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", migration.Id);
        command.Parameters.AddWithValue("sequence", migration.Sequence);
        command.Parameters.AddWithValue("checksum", migration.Checksum);
        command.Parameters.AddWithValue("kind", executionKind);
        command.Parameters.AddWithValue("duration", durationMs);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ValidateExistingBaselineShapeAsync(
        MySqlConnection connection,
        CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND (
                  (table_name = 'recipes' AND column_name IN (
                      'description', 'card_image_blob_name', 'calories_per_serving', 'protein_per_serving'))
                  OR (table_name = 'recipe_ingredients' AND column_name IN (
                      'display_order', 'featured_order'))
                  OR (table_name = 'account_closure_cleanup' AND column_name = 'status')
              );
            """;
        await using var command = new MySqlCommand(sql, connection);
        var matchedColumns = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (matchedColumns != 7)
        {
            throw new InvalidOperationException(
                "Existing schema does not match the required migration-007 baseline signature.");
        }
    }

    private static async Task AcquireLockAsync(
        MySqlConnection connection,
        string lockName,
        CancellationToken ct)
    {
        await using var command = new MySqlCommand("SELECT GET_LOCK(@name, 30);", connection);
        command.Parameters.AddWithValue("name", lockName);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new InvalidOperationException("Could not acquire the database migration lock.");
    }

    private static async Task ReleaseLockAsync(MySqlConnection connection, string lockName)
    {
        try
        {
            await using var command = new MySqlCommand("SELECT RELEASE_LOCK(@name);", connection);
            command.Parameters.AddWithValue("name", lockName);
            await command.ExecuteScalarAsync();
        }
        catch
        {
            // Closing the connection also releases the session advisory lock.
        }
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken ct)
    {
        await using var command = new MySqlCommand(sql, connection)
        {
            CommandTimeout = 120,
        };
        await command.ExecuteNonQueryAsync(ct);
    }

    private static (IReadOnlyList<MigrationDefinition>, string) LoadEmbeddedResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Migrations/", StringComparison.Ordinal))
            .Select(name =>
            {
                var id = name["Migrations/".Length..];
                var match = MigrationNamePattern.Match(id);
                if (!match.Success)
                    throw new InvalidOperationException($"Invalid embedded migration name '{id}'.");

                var sql = ReadResource(assembly, name);
                return new MigrationDefinition(
                    id,
                    int.Parse(match.Groups["sequence"].Value),
                    ComputeChecksum(sql),
                    sql);
            })
            .OrderBy(item => item.Sequence)
            .ToArray();

        var duplicateSequence = migrations
            .GroupBy(item => item.Sequence)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSequence is not null)
            throw new InvalidOperationException($"Duplicate migration sequence {duplicateSequence.Key:D3}.");

        return (migrations, ReadResource(assembly, "Baseline/01-schema.sql"));
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ComputeChecksum(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string StripLeadingComments(string sql) =>
        Regex.Replace(sql, @"\A(?:\s|--[^\r\n]*(?:\r?\n|\z)|/\*[\s\S]*?\*/)*", string.Empty);

    private sealed record MigrationDefinition(
        string Id,
        int Sequence,
        string Checksum,
        string Sql);
}

public static class SqlScriptParser
{
    public static IReadOnlyList<string> Split(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var statements = new List<string>();
        var buffer = new StringBuilder();
        var delimiter = ";";

        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
            {
                if (!ContainsOnlyCommentsAndWhitespace(buffer.ToString()))
                    throw new InvalidOperationException("DELIMITER changed in the middle of a SQL statement.");

                buffer.Clear();
                delimiter = trimmedLine["DELIMITER ".Length..].Trim();
                if (delimiter.Length == 0)
                    throw new InvalidOperationException("SQL DELIMITER cannot be empty.");
                continue;
            }

            buffer.AppendLine(line);
            var candidate = buffer.ToString().TrimEnd();
            if (!candidate.EndsWith(delimiter, StringComparison.Ordinal))
                continue;

            var statement = candidate[..^delimiter.Length].Trim();
            if (statement.Length > 0)
                statements.Add(statement);
            buffer.Clear();
        }

        if (!ContainsOnlyCommentsAndWhitespace(buffer.ToString()))
            throw new InvalidOperationException("SQL script ended with an unterminated statement.");

        return statements;
    }

    private static bool ContainsOnlyCommentsAndWhitespace(string value)
    {
        var withoutBlockComments = Regex.Replace(value, @"/\*[\s\S]*?\*/", string.Empty);
        var withoutLineComments = Regex.Replace(
            withoutBlockComments,
            @"^\s*--[^\r\n]*(?:\r?\n|$)",
            string.Empty,
            RegexOptions.Multiline);
        return string.IsNullOrWhiteSpace(withoutLineComments);
    }
}
