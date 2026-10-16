using System.Data;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using MySqlConnector;
using Appetee.Application.utils;

namespace Appetee.Api.Diagnostics;

/// <summary>Value-free exception diagnostics. Never reads Message, Data, SQL, URLs or file paths.</summary>
public static class SafeExceptionDetails
{
    public sealed record Cause(string Type, string Kind, string Hint, int? DatabaseErrorNumber,
        int? DependencyStatus, string[] Frames, Cause[] Inner);

    public static Cause Capture(Exception exception)
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var budget = 8;
        return Capture(exception, seen, ref budget);
    }

    public static string Fingerprint(Cause cause) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cause))))[..16].ToLowerInvariant();

    private static Cause Capture(Exception exception, HashSet<Exception> seen, ref int budget)
    {
        seen.Add(exception);
        budget--;
        var (kind, hint) = Classify(exception);
        var inner = new List<Cause>();
        IEnumerable<Exception> children = exception is AggregateException aggregate ? aggregate.InnerExceptions
            : exception.InnerException is { } singleInner ? [singleInner] : [];
        foreach (var child in children)
        {
            if (budget <= 0) break;
            if (!seen.Contains(child)) inner.Add(Capture(child, seen, ref budget));
        }
        var frames = new StackTrace(exception, true).GetFrames()?.OrderByDescending(frame =>
            frame.GetMethod()?.DeclaringType?.FullName?.StartsWith("Appetee.", StringComparison.Ordinal) == true).Take(12).Select(frame =>
        {
            var method = frame.GetMethod();
            var line = frame.GetFileLineNumber();
            return Bound((method?.DeclaringType?.FullName ?? "unknown") + "." + (method?.Name ?? "unknown"))
                + (line > 0 ? ":line " + line : "");
        }).ToArray() ?? [];
        return new(Bound(exception.GetType().FullName ?? exception.GetType().Name), kind, hint,
            exception is MySqlException sql ? sql.Number : null,
            exception is RequestFailedException azure ? azure.Status : null, frames, inner.ToArray());
    }

    private static (string Kind, string Hint) Classify(Exception exception) => exception switch
    {
        ApiException { Code: "csrf_invalid" } => ("security.csrf_rejected", "Check browser origin, cookie and token lifecycle without logging their values."),
        ApiException { StatusCode: 401 } => ("security.unauthenticated", "Check session restoration/expiry; never log cookies or identity claims."),
        ApiException { StatusCode: 403 } => ("security.forbidden", "Check the operation permission and browser-origin policy."),
        ApiException { StatusCode: 409 } => ("request.conflict", "Refresh and review confirmed state; do not replay a write with a newer revision."),
        ApiException { StatusCode: 400 } => ("request.invalid", "Check the documented request contract; submitted values are intentionally omitted."),
        MySqlException { Number: 1054 } => ("database.column_missing", "Compare deployed query columns with the migrated schema."),
        MySqlException { Number: 1146 } => ("database.table_missing", "Verify the runtime database target and migration history."),
        MySqlException { Number: 1044 or 1045 or 1142 or 1227 } => ("database.access_denied", "Verify runtime identity privileges; do not expose credentials."),
        MySqlException { Number: 1049 } => ("database.unknown_database", "Verify the configured database target."),
        MySqlException { Number: 1205 } => ("database.lock_timeout", "Inspect transaction contention; do not blindly replay writes."),
        MySqlException { Number: 1213 } => ("database.deadlock", "Inspect lock ordering and bounded rollback-safe retry policy."),
        MySqlException { Number: 1062 } => ("database.unique_constraint", "Check the operation and unique-constraint contract."),
        MySqlException { Number: 1451 or 1452 } => ("database.foreign_key", "Check relational integrity and operation ordering."),
        MySqlException => ("database.failure", "Use the database error number and application frames; never log SQL or parameters."),
        DataException or InvalidCastException or FormatException => ("data.mapping", "Inspect query aliases, nullability and CLR types at the recorded application frame."),
        RequestFailedException => ("dependency.azure", "Check the dependency status and managed-identity/resource configuration."),
        SocketException => ("dependency.network", "Check DNS, connectivity, firewall and service availability."),
        TimeoutException => ("dependency.timeout", "Check dependency latency and operation deadline; reconcile uncertain writes."),
        OperationCanceledException => ("operation.cancelled", "Distinguish a client disconnect from a server/dependency deadline."),
        _ => ("application.failure", "Use the exception type, preserved causes and application frames to locate the failure.")
    };

    private static string Bound(string value) => value.Length <= 256 ? value : value[..256];
}
