using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using Appetee.Application.Diagnostics;

namespace Appetee.Api.Diagnostics;

/// <summary>An allowlisted projection, not a regex scrubber of arbitrary log messages.</summary>
public sealed class SafeLogState : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly KeyValuePair<string, object?>[] fields;
    private SafeLogState(Dictionary<string, object?> values) => fields = values.ToArray();
    public int Count => fields.Length;
    public KeyValuePair<string, object?> this[int index] => fields[index];
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)fields).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => JsonSerializer.Serialize(fields.ToDictionary(pair => pair.Key, pair => pair.Value));

    private static readonly HashSet<string> NumericFields = new(StringComparer.Ordinal)
    {
        "StatusCode", "ResponseStatusCode", "Status", "ElapsedMs", "RetryCount", "Attempt", "RowsRead", "RowsWritten", "PlanRows",
        "DayRows", "MealRows", "ItemRows", "SqlCommands", "ReportedRows", "Count", "DeletedRows", "ExitCode"
    };
    private static readonly HashSet<string> BooleanFields = new(StringComparer.Ordinal)
        { "Changed", "ProfileChanged", "ResponseStarted", "TokenRemoved", "Deleted" };
    private static readonly HashSet<string> Outcomes = new(StringComparer.Ordinal)
        { "completed", "failed", "rejected", "cancelled", "success", "no-op", "unknown", "rolled_back" };

    public static SafeLogState Create<TState>(string category, LogLevel level, EventId eventId,
        TState state, Exception? exception, IExternalScopeProvider? scopes = null, TelemetrySettings? metadata = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["SchemaVersion"] = 1,
            ["TimestampUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["Service"] = "Appetee.Api",
            ["ServiceVersion"] = metadata?.ReleaseVersion ?? typeof(SafeLogState).Assembly.GetName().Version?.ToString(),
            ["DeploymentEnvironment"] = metadata?.Environment ?? "Unknown",
            ["Level"] = level.ToString(),
            ["Category"] = SafeCategory(category),
            ["EventId"] = eventId.Id,
            ["EventName"] = LogEvents.Name(eventId.Id) ?? (level >= LogLevel.Error ? "application.failure" : "application.event")
        };
        if (state is IEnumerable<KeyValuePair<string, object?>> structured)
        {
            foreach (var pair in structured.Take(64))
            {
                // No formatter, ToString(), arbitrary object serialization, messages or free-text scopes.
                if (NumericFields.Contains(pair.Key) && TryNumber(pair.Value, out var number)) values[pair.Key] = number;
                else if (BooleanFields.Contains(pair.Key) && pair.Value is bool flag) values[pair.Key] = flag;
                else if (pair.Key == "Outcome" && pair.Value is string outcome && Outcomes.Contains(outcome)) values[pair.Key] = outcome;
            }
        }
        scopes?.ForEachScope((scope, destination) =>
        {
            // Only the application-owned typed request context can supply request metadata.
            if (scope is RequestLogContext request)
            {
                destination["TraceId"] = request.TraceId;
                destination["SpanId"] = request.SpanId;
                destination["HttpMethod"] = request.HttpMethod;
                destination["Route"] = request.Route;
            }
        }, values);
        if (!values.ContainsKey("TraceId") && Activity.Current is { } activity)
        {
            values["TraceId"] = activity.TraceId.ToHexString();
            values["SpanId"] = activity.SpanId.ToHexString();
        }
        if (exception is not null)
        {
            var cause = SafeExceptionDetails.Capture(exception);
            var root = cause;
            while (root.Inner.Length > 0) root = root.Inner[0];
            values["ExceptionType"] = cause.Type;
            values["RootExceptionType"] = root.Type;
            values["FailureKind"] = root.Kind;
            values["FailureHint"] = root.Hint;
            values["FailureFingerprint"] = SafeExceptionDetails.Fingerprint(cause);
            values["ExceptionChain"] = cause;
        }
        return new(values);
    }

    private static bool TryNumber(object? value, out double number)
    {
        number = value switch { byte v => v, short v => v, int v => v, long v => v, float v => v, double v => v, decimal v => (double)v, _ => double.NaN };
        return double.IsFinite(number) && number >= 0 && number <= long.MaxValue;
    }

    internal static string SafeCategory(string category)
    {
        if (category == "Appetee.Startup" || category is "Microsoft.Hosting.Lifetime" or "Microsoft.AspNetCore.Hosting.Diagnostics") return category;
        if (category.Length > 180 || !category.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '+' or '`')) return "Other";
        // Prefix matching alone would leak a secret supplied as 'Appetee.<secret>'.
        // Approve only actual loaded application/framework/SDK type categories.
        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly => !assembly.IsDynamic
            && assembly.GetName().Name is { } name
            && (name.StartsWith("Appetee.", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal) || name.StartsWith("Azure.", StringComparison.Ordinal))
            && assembly.GetType(category, throwOnError: false) is not null) ? category : "Other";
    }
}
