using System.Text.Json;
using Appetee.DatabaseMigrator;

namespace Appetee.Api.Tests.Unit;

public sealed class MigrationFailureDiagnosticsTests
{
    [Fact]
    public void CliFailurePreservesDatabaseCodeWithoutMessagesOrData()
    {
        var failure = new InvalidOperationException(PrivacyLoggingTests.Secret, PrivacyLoggingTests.SqlFailure(1045));
        failure.Data["ConnectionString"] = PrivacyLoggingTests.Secret;
        using var output = new StringWriter();
        MigrationFailureDiagnostics.Write(failure, output);
        Assert.DoesNotContain(PrivacyLoggingTests.Secret, output.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("migration.failed", json.RootElement.GetProperty("EventName").GetString());
        Assert.Equal(1045, json.RootElement.GetProperty("ExceptionChain")[1].GetProperty("DatabaseErrorNumber").GetInt32());
    }

    [Fact]
    public void CliFailureBoundsAggregateAndNeverReadsMessage()
    {
        using var output = new StringWriter();
        MigrationFailureDiagnostics.Write(new AggregateException(Enumerable.Range(0, 100).Select(_ => new PoisonedException())), output);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(8, json.RootElement.GetProperty("ExceptionChain").GetArrayLength());
    }

    private sealed class PoisonedException : Exception
    {
        public override string Message => throw new InvalidOperationException("Exception messages must not be read.");
    }
}
