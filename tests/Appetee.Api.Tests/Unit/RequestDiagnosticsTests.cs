using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using Appetee.Api.Configuration;
using Appetee.Api.Diagnostics;
using Appetee.Api.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises the real middleware/error pipeline in TestServer without MySQL, Azure or live identities.</summary>
public sealed class RequestDiagnosticsTests
{
    private const string PrivateRouteValue = "CANARY_PRIVATE_ROUTE_91462";
    [Fact]
    public async Task FullPipelineCorrelatesFailureAndCompletionWithoutLeakingRequestOrExceptionValues()
    {
        var (app, capture) = await Host();
        await using var ownedApp = app;
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/diagnostics-test/" + PrivateRouteValue + "?token=" + PrivacyLoggingTests.Secret);
        request.Headers.Add("Origin", "https://client.test");
        request.Headers.Add("Authorization", "Bearer " + PrivacyLoggingTests.Secret);
        request.Headers.Add("X-Trace-Id", PrivacyLoggingTests.Secret);
        request.Headers.Add("Cookie", "__Host-appetee=" + PrivacyLoggingTests.Secret);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(PrivacyLoggingTests.Secret, body);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.ToString() ?? "");
        using var problem = JsonDocument.Parse(body);
        var trace = Assert.Single(response.Headers.GetValues(RequestDiagnosticsMiddleware.TraceHeader));
        Assert.Equal(trace, Assert.Single(response.Headers.GetValues(RequestDiagnosticsMiddleware.AppeteeTraceHeader)));
        Assert.Equal(trace, problem.RootElement.GetProperty("traceId").GetString());
        Assert.Contains("X-Trace-Id", string.Join(',', response.Headers.GetValues("Access-Control-Expose-Headers")));
        Assert.Contains("X-Appetee-Trace-Id", string.Join(',', response.Headers.GetValues("Access-Control-Expose-Headers")));
        var records = capture.Records.Select(record => JsonDocument.Parse(record.Json)).ToArray();
        try
        {
            var failure = Assert.Single(records, record => record.RootElement.GetProperty("EventName").GetString() == "http.exception.handled");
            Assert.Equal(trace, failure.RootElement.GetProperty("TraceId").GetString());
            Assert.Equal("database.column_missing", failure.RootElement.GetProperty("FailureKind").GetString());
            Assert.Equal("/api/diagnostics-test/{id}", failure.RootElement.GetProperty("Route").GetString());
            var completed = Assert.Single(records, record => record.RootElement.GetProperty("EventName").GetString() == "http.request.completed");
            Assert.Equal(500, completed.RootElement.GetProperty("StatusCode").GetInt32());
            Assert.Equal(trace, completed.RootElement.GetProperty("TraceId").GetString());
        }
        finally { foreach (var record in records) record.Dispose(); }
        Assert.All(capture.Records, record => {
            Assert.DoesNotContain(PrivacyLoggingTests.Secret, record.Json);
            Assert.DoesNotContain(PrivateRouteValue, record.Json);
        });
    }

    [Fact]
    public async Task SuccessUnauthorizedNotFoundAndCsrfFailuresKeepStatusAndSafeRequestSummaries()
    {
        var (app, capture) = await Host();
        await using var ownedApp = app;
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var success = await client.GetAsync("/api/diagnostics-ok");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(Assert.Single(success.Headers.GetValues(RequestDiagnosticsMiddleware.TraceHeader)),
            Assert.Single(success.Headers.GetValues(RequestDiagnosticsMiddleware.AppeteeTraceHeader)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/diagnostics-private")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/" + PrivacyLoggingTests.Secret)).StatusCode);
        using var mutation = new HttpRequestMessage(HttpMethod.Post, "/api/diagnostics-write")
        {
            Content = new StringContent("{\"password\":\"" + PrivacyLoggingTests.Secret + "\"}", Encoding.UTF8, "application/json")
        };
        mutation.Headers.Add("Origin", "https://client.test");
        var rejected = await client.SendAsync(mutation);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("csrf_invalid", await rejected.Content.ReadAsStringAsync());
        Assert.Contains(capture.Records, record => record.Json.Contains("security.csrf_rejected", StringComparison.Ordinal));
        Assert.All(capture.Records, record => Assert.DoesNotContain(PrivacyLoggingTests.Secret, record.Json));
    }

    [Fact]
    public async Task ClientCancellationIsNotReportedAsServerFailure()
    {
        var capture = new DiagnosticCaptureProvider();
        using var sinks = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var safe = new PrivacyLoggingProvider(sinks);
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(safe));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var http = new DefaultHttpContext { RequestAborted = cancelled.Token };
        var middleware = new RequestDiagnosticsMiddleware(_ => throw new OperationCanceledException(cancelled.Token), factory.CreateLogger<RequestDiagnosticsMiddleware>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(http));
        Assert.DoesNotContain(capture.Records, record => record.Level >= LogLevel.Error);
        Assert.Contains(capture.Records, record => record.Json.Contains("499", StringComparison.Ordinal));
    }

    private static async Task<(WebApplication App, DiagnosticCaptureProvider Capture)> Host()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://client.test" });
        var capture = new DiagnosticCaptureProvider();
        var sinks = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new PrivacyLoggingProvider(sinks));
        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
        builder.Services.AddAppeteeApi(builder.Configuration, builder.Environment);
        builder.Services.AddControllers().AddApplicationPart(typeof(Appetee.Api.Controllers.ErrorsController).Assembly);
        builder.Services.AddOptions<AppeteeAuthenticationOptions>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAppeteePipeline();
        app.MapGet("/api/diagnostics-test/{id}", (HttpContext _) => Task.FromException(new DataException(PrivacyLoggingTests.Secret, PrivacyLoggingTests.SqlFailure(1054))));
        app.MapGet("/api/diagnostics-ok", () => Results.Ok());
        app.MapGet("/api/diagnostics-private", () => Results.Ok()).RequireAuthorization();
        app.MapPost("/api/diagnostics-write", () => Results.Ok());
        await app.StartAsync();
        return (app, capture);
    }
}
