using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Data;
using System.Text.Json;
using Appetee.Api.Diagnostics;
using Appetee.Api.Tests.Unit;
using Appetee.Api.Extensions;
using Appetee.Api.Configuration;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Appetee.Api.Tests.Diagnostics;

[Collection("Process-wide telemetry")]
public sealed class TelemetryDistroPipelineTests
{
    [Fact]
    public async Task RealApiFailureHeaderProblemLogAndExportShareTraceWithoutLeakingPathOrQuery()
    {
        var transport = new MemoryTransport();
        var activityEvidence = new ConcurrentBag<string>();
        var serverEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.PropagationData,
            ActivityStopped = activity =>
            {
                activityEvidence.Add($"source={activity.Source.Name}; recorded={activity.Recorded}; events={activity.Events.Count()}; links={activity.Links.Count()}");
                if (activity.GetCustomProperty(SafeActivityProcessor.RequestContextKey) is RequestLogContext { Route: "/api/observability-test/{id}" })
                    serverEnded.TrySetResult();
            }
        };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Cors:AllowedOrigins:0"] = "https://client.test";
        builder.Configuration["Observability:Enabled"] = "true";
        builder.Configuration["Observability:ReleaseVersion"] = "e012-api-test";
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000001;IngestionEndpoint=https://example.invalid";
        builder.Services.AddAppeteeTelemetry(builder.Configuration, builder.Environment);
        builder.Services.PostConfigure<AzureMonitorOptions>(options => options.Transport = transport);
        builder.Logging.ClearProviders();
        var capture = new DiagnosticCaptureProvider();
        builder.Logging.AddProvider(new PrivacyLoggingProvider(LoggerFactory.Create(logging => logging.AddProvider(capture))));
        builder.Services.AddAppeteeApi(builder.Configuration, builder.Environment);
        builder.Services.AddControllers().AddApplicationPart(typeof(Appetee.Api.Controllers.ErrorsController).Assembly);
        builder.Services.AddOptions<AppeteeAuthenticationOptions>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAppeteePipeline();
        app.MapGet("/api/observability-test/{id}", () => Task.FromException(new DataException(PrivacyLoggingTests.Secret, PrivacyLoggingTests.SqlFailure(1054))));
        await app.StartAsync();
        ActivitySource.AddActivityListener(listener);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var response = await client.GetAsync("/api/observability-test/" + PrivacyLoggingTests.Secret + "?token=" + PrivacyLoggingTests.Secret);
        Assert.Equal(500, (int)response.StatusCode);
        var trace = Assert.Single(response.Headers.GetValues(RequestDiagnosticsMiddleware.AppeteeTraceHeader));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(trace, problem.RootElement.GetProperty("traceId").GetString());
        // TestServer can deliver the body before the server Activity ends. Flushing before
        // that end races an empty queue; wait for actual server completion, not a delay.
        await serverEnded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(app.Services.GetRequiredService<TracerProvider>().ForceFlush(5000));
        var payload = string.Join('\n', transport.Payloads);
        Assert.True(payload.Contains(trace), "No matching exported trace; safe activity evidence: " + string.Join(" | ", activityEvidence));
        Assert.Contains("/api/observability-test/{id}", payload);
        Assert.DoesNotContain(PrivacyLoggingTests.Secret, payload);
        Assert.All(capture.Records, record => Assert.DoesNotContain(PrivacyLoggingTests.Secret, record.Json));
        Assert.Contains(capture.Records, record => record.Json.Contains(trace) && record.Json.Contains("database.column_missing"));
        AssertSafeDependencies(app.Services.GetRequiredService<TracerProvider>(), transport);
    }

    private static void AssertSafeDependencies(TracerProvider tracer, MemoryTransport transport)
    {
        using var source = new ActivitySource("System.Net.Http");
        string traceId;
        using (var activity = source.StartActivity(PrivacyLoggingTests.Secret, ActivityKind.Client))
        {
            Assert.NotNull(activity);
            traceId = activity.TraceId.ToHexString();
            activity.SetTag("url.full", "https://example.test/" + PrivacyLoggingTests.Secret + "?password=" + PrivacyLoggingTests.Secret);
            activity.SetTag("http.request.method", "GET");
            activity.SetTag("http.response.status_code", 503);
            activity.AddBaggage("password", PrivacyLoggingTests.Secret);
            activity.SetStatus(ActivityStatusCode.Error, PrivacyLoggingTests.Secret);
        }
        using (var rejected = source.StartActivity("must-not-export"))
        {
            Assert.NotNull(rejected);
            rejected.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { ["exception.message"] = PrivacyLoggingTests.Secret }));
        }
        Assert.True(tracer.ForceFlush(5000));
        var payload = string.Join('\n', transport.Payloads);
        Assert.NotEmpty(payload);
        Assert.Contains(traceId, payload);
        Assert.Contains("appetee-api", payload);
        Assert.Contains("e012-api-test", payload);
        Assert.Contains("503", payload);
        Assert.DoesNotContain("must-not-export", payload);
        Assert.DoesNotContain(PrivacyLoggingTests.Secret, payload);
        Assert.DoesNotContain("example.test", payload);
    }

    private sealed class MemoryTransport : HttpPipelineTransport
    {
        public ConcurrentBag<string> Payloads { get; } = [];
        public override Request CreateRequest() => HttpClientTransport.Shared.CreateRequest();
        public override void Process(HttpMessage message)
        {
            using var stream = new MemoryStream();
            message.Request.Content?.WriteTo(stream, CancellationToken.None);
            stream.Position = 0;
            if (stream.Length > 1 && stream.ReadByte() == 0x1f && stream.ReadByte() == 0x8b)
            {
                stream.Position = 0;
                using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                Payloads.Add(reader.ReadToEnd());
            }
            else Payloads.Add(Encoding.UTF8.GetString(stream.ToArray()));
            message.Response = new AcceptedResponse();
        }
        public override ValueTask ProcessAsync(HttpMessage message) { Process(message); return ValueTask.CompletedTask; }
    }

    private sealed class AcceptedResponse : Response
    {
        public override int Status => 200;
        public override string ReasonPhrase => "OK";
        public override Stream? ContentStream { get; set; } = new MemoryStream(Encoding.UTF8.GetBytes("{\"itemsReceived\":1,\"itemsAccepted\":1,\"errors\":[]}"));
        public override string ClientRequestId { get; set; } = "test";
        public override void Dispose() => ContentStream?.Dispose();
        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];
        protected override bool TryGetHeader(string name, out string value) { value = string.Empty; return false; }
        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values) { values = []; return false; }
    }
}

// ASP.NET/HttpClient instrumentation and AppContext switches are process-wide. Multiple
// simultaneous distro hosts route DiagnosticSource callbacks to conflicting providers.
// Isolate just these provider-owning tests; do not disable parallelism for the suite.
[CollectionDefinition("Process-wide telemetry", DisableParallelization = true)]
public sealed class ProcessWideTelemetryCollection;
