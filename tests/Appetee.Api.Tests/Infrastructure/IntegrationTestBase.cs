using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<AppeteeWebApplicationFactory>, IAsyncLifetime
{
    protected IntegrationTestBase(AppeteeWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected AppeteeWebApplicationFactory Factory { get; }

    protected HttpClient Client { get; private set; } = default!;

    public virtual async Task InitializeAsync()
    {
        await Factory.Database.ResetAsync();
        Client = Factory.CreateApiClient();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient() => Factory.CreateApiClient();

    protected async Task<(HttpClient Client, AuthResult Result)> CreateAuthenticatedClientAsync(
        string? username = null,
        string? email = null,
        string password = "Password123!")
    {
        var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new SignUpRequest(
            Username: username ?? $"integration_{suffix}",
            Email: email ?? $"integration_{suffix}@appetee.test",
            Password: password,
            DietIds: new[] { 1, 2 },
            IngredientRestrictionIds: new[] { 4 });

        var response = await client.PostAsJsonAsync("/api/auth/sign-up", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Sign-up failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return (client, result!);
    }
}
