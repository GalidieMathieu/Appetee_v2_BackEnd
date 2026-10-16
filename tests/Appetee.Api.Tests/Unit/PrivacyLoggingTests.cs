using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Appetee.Api.Diagnostics;
using Appetee.Api.Helpers;
using Appetee.Application.Diagnostics;
using Appetee.Application.utils;
using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Appetee.Api.Tests.Unit;

public sealed class PrivacyLoggingTests
{
    internal const string Secret = "CANARY_PRIVATE_PASSWORD_74821";

    [Fact]
    public void ProviderNeverForwardsRawMessagesExceptionsObjectsOrScopes()
    {
        var capture = new DiagnosticCaptureProvider();
        using var sinks = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        using var safe = new PrivacyLoggingProvider(sinks);
        using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(safe));
        var logger = factory.CreateLogger("Appetee.Test");
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["Authorization"] = Secret, ["TraceId"] = Secret });
        var formatterCalled = false;
        var state = new KeyValuePair<string, object?>[] {
            new("Password", Secret), new("{OriginalFormat}", Secret), new("ElapsedMs", 12.5), new("StatusCode", 500),
            new("Email", Secret + "@example.test"), new("UserId", 27), new("Body", new PoisonedObject()),
            new("ElapsedMs", double.PositiveInfinity), new("Operation", Secret), new("Outcome", Secret)
        };
        logger.Log(LogLevel.Error, LogEvents.RequestFailed, state, new RequestFailedException(403, Secret), (_, _) => { formatterCalled = true; throw new Exception(Secret); });
        var record = Assert.Single(capture.Records);
        Assert.False(formatterCalled);
        Assert.Null(record.Exception);
        Assert.IsType<SafeLogState>(record.State);
        Assert.DoesNotContain(Secret, record.Json);
        Assert.DoesNotContain("UserId", record.Json);
        Assert.DoesNotContain("Authorization", record.Json);
        using var json = JsonDocument.Parse(record.Json);
        Assert.Equal(500, json.RootElement.GetProperty("StatusCode").GetInt32());
        Assert.Equal("dependency.azure", json.RootElement.GetProperty("FailureKind").GetString());
    }

    [Theory]
    [InlineData(1054, "database.column_missing")]
    [InlineData(1146, "database.table_missing")]
    [InlineData(1045, "database.access_denied")]
    [InlineData(1213, "database.deadlock")]
    [InlineData(1205, "database.lock_timeout")]
    [InlineData(1062, "database.unique_constraint")]
    [InlineData(1452, "database.foreign_key")]
    public void DatabaseClassificationKeepsCodesButNotMessagesOrSql(int code, string expected)
    {
        var original = SqlFailure(code);
        original.Data["ConnectionString"] = Secret;
        var wrapper = new InternalServerException(inner: new DataException(Secret, original));
        var details = SafeExceptionDetails.Capture(wrapper);
        var root = Assert.Single(Assert.Single(details.Inner).Inner);
        Assert.Equal(expected, root.Kind);
        Assert.Equal(code, root.DatabaseErrorNumber);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(details));
        Assert.Equal(SafeExceptionDetails.Fingerprint(details), SafeExceptionDetails.Fingerprint(details));
    }

    [Fact]
    public void CauseGraphIsBoundedAndDoesNotReadCustomExceptionMessages()
    {
        var exception = new AggregateException(Enumerable.Range(0, 200).Select(_ => new PoisonedException()));
        var details = SafeExceptionDetails.Capture(exception);
        Assert.Equal(7, details.Inner.Length);
        Assert.All(details.Inner, cause => Assert.True(cause.Frames.Length <= 12));
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(details));
    }

    [Fact]
    public void StartupFallbackIsOneSafeJsonRecordWithoutRawExceptionText()
    {
        using var output = new StringWriter();
        StartupDiagnostics.WriteFailure(new InvalidOperationException(Secret, SqlFailure(1045)), output);
        var text = output.ToString();
        Assert.DoesNotContain(Secret, text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("app.startup.failed", json.RootElement.GetProperty("EventName").GetString());
        Assert.Equal("database.access_denied", json.RootElement.GetProperty("FailureKind").GetString());
    }

    [Fact]
    public void StartupStillReportsSafelyWhenItsProviderIsUnavailable()
    {
        using var output = new StringWriter();
        StartupDiagnostics.ReportFailure(new InvalidOperationException(Secret, SqlFailure(1045)), new FailingProvider(), output);
        Assert.DoesNotContain(Secret, output.ToString());
        Assert.Contains("database.access_denied", output.ToString());
    }

    [Fact]
    public void UnknownCategoryNamesAreNotAnotherSecretChannel()
    {
        var capture = new DiagnosticCaptureProvider();
        using var sinks = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var safe = new PrivacyLoggingProvider(sinks);
        safe.CreateLogger("Appetee." + Secret).LogError(Secret);
        var record = Assert.Single(capture.Records);
        Assert.DoesNotContain(Secret, record.Json);
        Assert.Contains("Other", record.Json);
    }

    [Theory]
    [InlineData(typeof(MealPlannerBoundaryAttribute))]
    [InlineData(typeof(NutritionPlanBoundaryAttribute))]
    [InlineData(typeof(CatalogueBoundaryAttribute))]
    public async Task BoundariesPreserveUnexpectedCausesAndDoNotChangeKnownExceptions(Type filterType)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = "GET";
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor
            { RouteValues = new Dictionary<string, string?> { ["action"] = "GetRange" } });
        var filter = (ActionFilterAttribute)Activator.CreateInstance(filterType)!;
        var executing = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var original = new DataException(Secret);
        var executed = new ActionExecutedContext(action, [], new object()) { Exception = original };
        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        Assert.Same(original, Assert.IsType<InternalServerException>(executed.Exception).InnerException);

        var known = new ValidationException("Safe public message", "validation_failed", new Dictionary<string, string[]>());
        executed = new ActionExecutedContext(action, [], new object()) { Exception = known };
        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));
        Assert.Same(known, executed.Exception);
    }

    [Fact]
    public async Task ConcurrentTypedScopesStayIsolatedAndFreeTextScopesAreIgnored()
    {
        var capture = new DiagnosticCaptureProvider();
        using var sinks = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var safe = new PrivacyLoggingProvider(sinks);
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(safe));
        var logger = factory.CreateLogger("Appetee.Test");
        var traces = await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
            var context = new RequestLogContext("GET");
            var http = new DefaultHttpContext();
            http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(index % 2 == 0 ? "/one/{id}" : "/two/{id}"), 0, EndpointMetadataCollection.Empty, "test"));
            context.CaptureEndpoint(http);
            using var scope = logger.BeginScope(context);
            using var secretScope = logger.BeginScope(Secret);
            await Task.Yield();
            logger.LogInformation(LogEvents.RequestCompleted, Secret);
            return (context.TraceId, context.Route);
        }));
        Assert.Equal(20, capture.Records.Count);
        foreach (var trace in traces)
        {
            var record = Assert.Single(capture.Records, record => record.Json.Contains(trace.TraceId, StringComparison.Ordinal));
            using var json = JsonDocument.Parse(record.Json);
            Assert.Equal(trace.Route, json.RootElement.GetProperty("Route").GetString());
            Assert.DoesNotContain(Secret, record.Json);
        }
    }

    internal static MySqlException SqlFailure(int number) => (MySqlException)typeof(MySqlException)
        .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(MySqlErrorCode), typeof(string), typeof(string)], null)!
        .Invoke([(MySqlErrorCode)number, "42S22", Secret]);

    private sealed class PoisonedObject { public override string ToString() => throw new Exception(Secret); }
    private sealed class PoisonedException : Exception { public override string Message => throw new Exception(Secret); }
    private sealed class FailingProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException(Secret);
        public void Dispose() { }
    }
}

internal sealed class DiagnosticCaptureProvider : ILoggerProvider
{
    internal sealed record Record(LogLevel Level, string Json, object? State, Exception? Exception);
    public ConcurrentQueue<Record> Records { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
    public void Dispose() { }
    private sealed class CaptureLogger(DiagnosticCaptureProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Records.Enqueue(new(logLevel, formatter(state, exception), state, exception));
    }
}
