using Appetee.DatabaseMigrator;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B01MigrationRunnerTests
{
    [Fact]
    public void SqlScriptParser_PreservesStoredProcedureBodiesAcrossDelimiterChanges()
    {
        const string script = """
            CREATE TABLE sample (id INT);
            -- A migration comment may precede its delimiter declaration.
            DELIMITER $$
            CREATE PROCEDURE sample_procedure()
            BEGIN
                INSERT INTO sample (id) VALUES (1);
                INSERT INTO sample (id) VALUES (2);
            END$$
            DELIMITER ;
            DROP PROCEDURE sample_procedure;
            """;

        var statements = SqlScriptParser.Split(script);

        Assert.Collection(
            statements,
            statement => Assert.StartsWith("CREATE TABLE", statement, StringComparison.Ordinal),
            statement =>
            {
                Assert.StartsWith("CREATE PROCEDURE", statement, StringComparison.Ordinal);
                Assert.Contains("VALUES (1);", statement, StringComparison.Ordinal);
                Assert.Contains("VALUES (2);", statement, StringComparison.Ordinal);
            },
            statement => Assert.StartsWith("DROP PROCEDURE", statement, StringComparison.Ordinal));
    }

    [Fact]
    public void SqlScriptParser_RejectsAnUnterminatedScript()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlScriptParser.Split("CREATE TABLE sample (id INT)"));

        Assert.Contains("unterminated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MigrationRunner_LoadsTheEmbeddedBaselineAndOrderedMigrations()
    {
        var exception = Record.Exception(() => new MigrationRunner(
            "Server=127.0.0.1;Database=appetee_backend_tests;User ID=test;Password=test;",
            TextWriter.Null));

        Assert.Null(exception);
    }
}
