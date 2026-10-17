using System.Text.RegularExpressions;
using Appetee.DatabaseMigrator;

namespace Appetee.Api.Tests.Unit;

public sealed class SchemaRepairMigrationTests
{
    private static string Resource(string name)
    {
        using var stream = typeof(MigrationRunner).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing embedded SQL resource.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void RepairIsPackagedAndContainsOnlyPreflightAndThreeCreateStatements()
    {
        var sql = Resource("Migrations/20261016_011_repair_missing_application_tables.sql");
        var statements = SqlScriptParser.Split(sql);
        Assert.Equal(4, statements.Count);
        Assert.StartsWith("CREATE PROCEDURE", statements[1]);
        Assert.Contains("SIGNAL SQLSTATE '45000'", statements[1]);
        Assert.StartsWith("CALL repair_missing_application_tables_011()", statements[2]);
        Assert.Equal(3, Regex.Matches(statements[1], "CREATE TABLE IF NOT EXISTS").Count);
        Assert.StartsWith("DROP PROCEDURE repair_missing_application_tables_011", statements[3]);
        var executable = Regex.Replace(sql, @"(?m)^\s*--[^\r\n]*", "");
        Assert.DoesNotMatch(@"(?i)\b(USE|TRUNCATE|DELETE|INSERT|UPDATE|GRANT|REVOKE|ALTER)\s+(?:TABLE\s+)?\w+", executable.Replace("ON UPDATE RESTRICT", "").Replace("ON UPDATE CURRENT_TIMESTAMP", "").Replace("ON DELETE CASCADE", ""));
        Assert.DoesNotMatch(@"(?i)DROP\s+(TABLE|DATABASE)", executable);
    }

    [Theory]
    [InlineData("meal_plan_manual_items")]
    [InlineData("account_closure_cleanup")]
    [InlineData("account_permissions")]
    public void RepairDefinitionsMatchCanonicalFreshDatabase(string table)
    {
        var repair = Resource("Migrations/20261016_011_repair_missing_application_tables.sql");
        var schema = Resource("Baseline/01-schema.sql");
        string Definition(string sql)
        {
            var match = Regex.Match(sql, $@"CREATE TABLE(?: IF NOT EXISTS)? {table} \([\s\S]*?ENGINE=InnoDB[^;]*;");
            Assert.True(match.Success);
            return Regex.Replace(match.Value.Replace("IF NOT EXISTS ", ""), @"\s+", " ");
        }
        Assert.Equal(Definition(schema), Definition(repair));
    }
}
