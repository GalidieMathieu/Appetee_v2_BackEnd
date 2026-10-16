using System.Text.Json;
using MySqlConnector;

namespace Appetee.DatabaseMigrator;

/// <summary>Standalone CLI diagnostics without messages, arguments or connection settings.</summary>
public static class MigrationFailureDiagnostics
{
    public static void Write(Exception exception, TextWriter output)
    {
        var causes = new List<object>();
        var pending = new Queue<Exception>();
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Enqueue(exception);
        while (pending.Count > 0 && causes.Count < 8)
        {
            var cause = pending.Dequeue();
            if (!seen.Add(cause)) continue;
            causes.Add(new
            {
                Type = cause.GetType().FullName,
                DatabaseErrorNumber = cause is MySqlException sql ? (int?)sql.Number : null
            });
            // Enqueue only the bounded diagnostic budget, even for large aggregates.
            IEnumerable<Exception> children = cause is AggregateException aggregate
                ? aggregate.InnerExceptions : cause.InnerException is { } inner ? [inner] : [];
            foreach (var child in children.Take(Math.Max(0, 8 - causes.Count - pending.Count)))
                pending.Enqueue(child);
        }
        output.WriteLine(JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            TimestampUtc = DateTimeOffset.UtcNow,
            Service = "Appetee.DatabaseMigrator",
            Level = "Error",
            EventName = "migration.failed",
            FailureHint = "Check command syntax, migration history, database error numbers and runtime privileges using the migration runbook. Never share connection settings or command arguments.",
            ExceptionChain = causes
        }));
        output.Flush();
    }
}
