using Appetee.Api.Extensions;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddAppeteeBackend();

    var app = builder.Build();
    app.UseAppeteePipeline();
    app.Run();
}
catch (Exception ex)
{
    // Startup failures happen before the HTTP exception handler is available.
    var environmentName = Environment.GetEnvironmentVariable(
        "ASPNETCORE_ENVIRONMENT") ?? "Production";

    Console.Error.WriteLine(
        $"Application startup failed in environment '{environmentName}'.");
    Console.Error.WriteLine(ex);
    throw;
}

// Expose Program for WebApplicationFactory integration tests.
public partial class Program
{
}
