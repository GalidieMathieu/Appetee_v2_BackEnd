using System.Text.Json;

namespace Appetee.DatabaseMigrator;

/// <summary>Loads explicit migrator settings, without inheriting API credentials.</summary>
public static class MigrationSettings
{
    public const string SettingsFileName = "appsettings.migrations.json";
    public const string ConnectionVariable = "ConnectionStrings__AppeteeMigrationDb";

    public static string LoadConnectionString(string settingsDirectory,
        Func<string, string?>? readEnvironment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        var path = Path.Combine(settingsDirectory, SettingsFileName);
        if (!File.Exists(path))
        {
            // Preserve environment-only deployment compatibility, but never let
            // a stale environment value override an explicitly present JSON file.
            var legacy = (readEnvironment ?? Environment.GetEnvironmentVariable)(ConnectionVariable);
            if (!string.IsNullOrWhiteSpace(legacy)) return legacy;
            throw MissingSettings();
        }

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("ConnectionStrings", out var strings)
                || strings.ValueKind != JsonValueKind.Object
                || !strings.TryGetProperty("AppeteeMigrationDb", out var connection)
                || connection.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(connection.GetString()))
                throw MissingSettings();
            return connection.GetString()!;
        }
        catch (JsonException)
        {
            // Do not echo malformed JSON or credential values into console logs.
            throw new InvalidOperationException("The migrator's appsettings.migrations.json is not valid JSON.");
        }
    }

    private static InvalidOperationException MissingSettings() => new(
        "Set ConnectionStrings:AppeteeMigrationDb in the migrator's appsettings.migrations.json " +
        "(use appsettings.migrations.example.json as the template). The connection must name the intended database.");
}
