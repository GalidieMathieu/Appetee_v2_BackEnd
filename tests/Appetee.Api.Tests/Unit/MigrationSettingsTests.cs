using Appetee.DatabaseMigrator;
using System.Text.Json;

namespace Appetee.Api.Tests.Unit;

public sealed class MigrationSettingsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("appetee-migration-settings-").FullName;
    private const string LocalConnection = "Server=localhost;Database=appetee;User ID=synthetic;Password=synthetic-secret;";

    [Fact]
    public void JsonSettings_AreAuthoritativeOverEnvironmentAndIndependentOfWorkingDirectory()
    {
        Write(JsonSerializer.Serialize(new { ConnectionStrings = new { AppeteeMigrationDb = LocalConnection } }));
        Assert.Equal(LocalConnection, MigrationSettings.LoadConnectionString(_directory,
            _ => throw new InvalidOperationException("JSON must not read environment settings.")));
    }

    [Fact]
    public void MissingJson_PreservesExistingEnvironmentOnlyDeployment()
    {
        Assert.Equal(LocalConnection, MigrationSettings.LoadConnectionString(_directory, name =>
        { Assert.Equal(MigrationSettings.ConnectionVariable, name); return LocalConnection; }));
    }

    [Fact]
    public void MissingJsonAndEnvironment_FailsWithActionableMessage()
    {
        var error = Assert.Throws<InvalidOperationException>(() => MigrationSettings.LoadConnectionString(_directory, _ => null));
        Assert.Contains(MigrationSettings.SettingsFileName, error.Message);
        Assert.Contains("ConnectionStrings:AppeteeMigrationDb", error.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"ConnectionStrings\":null}")]
    [InlineData("{\"ConnectionStrings\":{\"AppeteeMigrationDb\":null}}")]
    [InlineData("{\"ConnectionStrings\":{\"AppeteeMigrationDb\":42}}")]
    [InlineData("{\"ConnectionStrings\":{\"AppeteeMigrationDb\":\"  \"}}")]
    public void PresentButInvalidSettings_FailClosedWithoutFallingBackToAnotherDatabase(string json)
    {
        Write(json);
        var consultedEnvironment = false;
        Assert.Throws<InvalidOperationException>(() => MigrationSettings.LoadConnectionString(_directory,
            _ => { consultedEnvironment = true; return LocalConnection; }));
        Assert.False(consultedEnvironment);
    }

    [Fact]
    public void MalformedJson_DoesNotExposeCredentialContent()
    {
        Write("{\"ConnectionStrings\":{\"AppeteeMigrationDb\":\"synthetic-secret");
        var error = Assert.Throws<InvalidOperationException>(() => MigrationSettings.LoadConnectionString(_directory));
        Assert.Equal("The migrator's appsettings.migrations.json is not valid JSON.", error.Message);
        Assert.DoesNotContain("synthetic-secret", error.ToString());
    }

    [Fact]
    public void JsonSettings_AcceptCommentsAndTrailingCommas()
    {
        Write("""
            {
              // Dedicated local migration account.
              "ConnectionStrings": {
                "AppeteeMigrationDb": "Server=localhost;Database=appetee;User ID=synthetic;",
              },
            }
            """);
        Assert.Contains("Database=appetee", MigrationSettings.LoadConnectionString(_directory));
    }

    private void Write(string json) => File.WriteAllText(Path.Combine(_directory, MigrationSettings.SettingsFileName), json);
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
