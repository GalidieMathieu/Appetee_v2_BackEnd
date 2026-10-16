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
        Factory.RecoveryEmailSender.Reset();
        Factory.MealPlanDiagnostics.Clear();
        Factory.MealPlanReplyFault.Clear();
        Client = Factory.CreateApiClient();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient() => Factory.CreateApiClient();

    /// <summary>Explicit synthetic admin provisioning; ordinary authenticated clients never receive grants.</summary>
    protected async Task<(HttpClient Client, AuthResult Result)> CreateCatalogueClientAsync(
        bool includeSeedRecipePrecondition = false, params string[] permissions)
    {
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var email=$"catalogue_{suffix}@appetee.test";
        var result=await CreateAuthenticatedClientAsync(email:email);
        var provisioner=new Appetee.DatabaseMigrator.PermissionOperator(Factory.Database.ConnectionString);
        foreach(var permission in permissions.Length==0 ? new[]{"Catalog.ReadAdmin","Catalog.Write"} : permissions)
            await provisioner.ChangeAsync(result.Result.userId,permission,true,"synthetic-test-operator",null,default);
        using var login=await result.Client.PostAsJsonAsync("/api/auth/login",new LoginRequest(email,"Password123!"));
        login.EnsureSuccessStatusCode();
        if (includeSeedRecipePrecondition)
            result.Client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match","\"catalog-recipe-1-v1\"");
        return result;
    }

    protected async Task<(HttpClient Client, AuthResult Result)> CreateAuthenticatedClientAsync(
        string? username = null,
        string? email = null,
        string password = "Password123!",
        IReadOnlyList<int>? dietIds = null,
        IReadOnlyList<int>? ingredientRestrictionIds = null)
    {
        var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new SignUpRequest(
            Username: username ?? $"integration_{suffix}",
            Email: email ?? $"integration_{suffix}@appetee.test",
            Password: password,
            DietIds: dietIds ?? new[] { 1, 2 },
            IngredientRestrictionIds: ingredientRestrictionIds ?? new[] { 4 });

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
