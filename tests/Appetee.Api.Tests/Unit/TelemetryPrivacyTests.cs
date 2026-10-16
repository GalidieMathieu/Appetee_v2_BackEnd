using System.Diagnostics;
using System.Text.Json;
using Appetee.Api.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Appetee.Api.Tests.Unit;

[Collection("Process-wide telemetry")]
public sealed class TelemetryPrivacyTests
{
    [Fact]
    public void DisabledExportNeverNeedsConnectionSettingsAndResourceIgnoresArbitraryEnvironmentAttributes()
    {
        var config = Config(new() { ["OTEL_RESOURCE_ATTRIBUTES"] = "user.email=" + PrivacyLoggingTests.Secret });
        var settings = TelemetrySettings.Read(config, new HostEnvironment());
        Assert.False(settings.Enabled);
        var resource = settings.Resource().Build().Attributes.ToDictionary(item => item.Key, item => item.Value);
        Assert.Equal("appetee-api", resource["service.name"]);
        Assert.Equal("Production", resource["deployment.environment"]);
        Assert.DoesNotContain(PrivacyLoggingTests.Secret, JsonSerializer.Serialize(resource));
    }

    [Theory]
    [InlineData(null, "Production", false)]
    [InlineData("InstrumentationKey=00000000-0000-0000-0000-000000000001", "Development", false)]
    public void EnabledExportRejectsMissingConfigurationOrAccidentalDevelopmentExport(string? connection, string stage, bool approval)
    {
        var config = Config(new() { ["Observability:Enabled"] = "true", ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = connection,
            ["Observability:AllowDevelopmentExport"] = approval.ToString() });
        var error = Assert.Throws<InvalidOperationException>(() => TelemetrySettings.Read(config, new HostEnvironment { EnvironmentName = stage }));
        Assert.DoesNotContain(connection ?? PrivacyLoggingTests.Secret, error.Message);
    }

    [Fact]
    public void ProductionExportRequiresExplicitReleaseMetadata()
    {
        var config = Config(new() { ["Observability:Enabled"] = "true",
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000001" });
        var error = Assert.Throws<InvalidOperationException>(() => TelemetrySettings.Read(config, new HostEnvironment()));
        Assert.Contains("release identifier", error.Message);
    }

    [Fact]
    public void SafeSpansReachBatchExporterWithoutQuerySqlStatusMessageBaggageOrConcreteRouteValues()
    {
        var capture = new SpanCapture();
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource("Microsoft.AspNetCore")
            .AddProcessor(new SafeActivityProcessor()).AddProcessor(new SimpleActivityExportProcessor(capture)).Build();
        using var source = new ActivitySource("Microsoft.AspNetCore");
        var http = new DefaultHttpContext();
        http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/api/meal-plan/days/{id}"), 0, EndpointMetadataCollection.Empty, "test"));
        var request = new RequestLogContext("GET");
        request.CaptureEndpoint(http);
        using (var span = source.StartActivity(PrivacyLoggingTests.Secret, ActivityKind.Server))
        {
            Assert.NotNull(span);
            span.SetCustomProperty(SafeActivityProcessor.RequestContextKey, request);
            span.SetTag("url.full", "https://invalid.test/" + PrivacyLoggingTests.Secret + "?token=" + PrivacyLoggingTests.Secret);
            span.SetTag("db.statement", PrivacyLoggingTests.Secret);
            span.SetTag("http.response.status_code", 500);
            span.AddBaggage("token", PrivacyLoggingTests.Secret);
            span.SetStatus(ActivityStatusCode.Error, PrivacyLoggingTests.Secret);
        }
        var record = Assert.Single(capture.Items);
        Assert.DoesNotContain(PrivacyLoggingTests.Secret, record);
        Assert.Contains("/api/meal-plan/days/{id}", record);
        Assert.Contains("500", record);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EventsAndUnreviewedSourcesAreDroppedRatherThanExported(bool eventPayload)
    {
        var capture = new SpanCapture();
        var name = eventPayload ? "Microsoft.AspNetCore" : "Unreviewed.Sql";
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource(name).AddProcessor(new SafeActivityProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(capture)).Build();
        using var source = new ActivitySource(name);
        using (var activity = source.StartActivity("test"))
        {
            Assert.NotNull(activity);
            if (eventPayload) activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { ["exception.message"] = PrivacyLoggingTests.Secret }));
        }
        Assert.Empty(capture.Items);
    }

    [Theory]
    [InlineData("System.Runtime", "dotnet.gc.collections", true)]
    [InlineData("Microsoft.AspNetCore.Hosting", "http.server.request.duration", true)]
    [InlineData("System.Net.Http", "http.client.request.duration", true)]
    [InlineData("MySqlConnector", "db.client.connections.usage", false)]
    [InlineData("Unknown", "secret.metric", false)]
    public void OnlyApprovedMetricsCanExport(string meter, string instrument, bool approved) =>
        Assert.Equal(approved, ObservabilityRegistration.ApprovedMetric(meter, instrument));

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Appetee.Api";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class SpanCapture : BaseExporter<Activity>
    {
        public List<string> Items { get; } = [];
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
                Items.Add(JsonSerializer.Serialize(new { activity.DisplayName, activity.StatusDescription, activity.TraceStateString,
                    Tags = activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value), Baggage = activity.Baggage.ToArray() }));
            return ExportResult.Success;
        }
    }
}
