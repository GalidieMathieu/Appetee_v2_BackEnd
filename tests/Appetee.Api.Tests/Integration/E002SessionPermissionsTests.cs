using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Abstractions.Catalogue;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Catalogue;
using Appetee.Application.Requests.Auth;
using Appetee.DatabaseMigrator;
using Appetee.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Appetee.Api.Tests.Integration;

public sealed class E002SessionPermissionsTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("")]
    [InlineData("Catalog.ReadAdmin")]
    [InlineData("Catalog.Write")]
    [InlineData("Catalog.ReadAdmin,Catalog.Write")]
    public async Task Session_ReturnsOnlyCurrentDurableGrants_WithoutChangingExistingFields(string grants)
    {
        var expected = grants.Length == 0 ? Array.Empty<string>() : grants.Split(',');
        var (client, user) = expected.Length == 0
            ? await CreateAuthenticatedClientAsync()
            : await CreateCatalogueClientAsync(permissions: expected);
        using (client)
        using (var response = await client.GetAsync("/api/auth/session"))
        {
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl!.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(new[] { "permissions", "userId", "username" },
                json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
            var session = json.RootElement.Deserialize<UserSessionDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(user.userId, session.userId);
            Assert.Equal(user.userName, session.Username);
            Assert.Equal(expected, session.Permissions);
            Assert.Equal(expected.Length, await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM account_permissions WHERE user_id=@Id;", new { Id=user.userId }));
            Assert.Equal(expected.Length, await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM administrative_audit;")); // Session reads add no grants or audit mutations.
        }
    }

    [Fact]
    public async Task Session_AnonymousIs401AndNotCacheable()
    {
        using var response = await Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("userId=1")]
    [InlineData("ownerId=1")]
    [InlineData("permissions=Catalog.Write")]
    public async Task Session_RejectsSelectorsAndClientPermissionInput(string query)
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using (client)
        using (var response = await client.GetAsync("/api/auth/session?" + query))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM account_permissions WHERE user_id=@Id;", new { Id=user.userId }));
        }
    }

    [Fact]
    public async Task Session_RevocationRejectsOldCookie_AndFreshLoginReturnsRemainingGrant()
    {
        var (client, user) = await CreateCatalogueClientAsync();
        using (client)
        {
            var permissionOperator = new PermissionOperator(Factory.Database.ConnectionString);
            Assert.True(await permissionOperator.ChangeAsync(user.userId, CataloguePermissions.Write,
                false, "synthetic-session-test-operator", null, default));
            using var stale = await client.GetAsync("/api/auth/session");
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            Assert.True(stale.Headers.CacheControl!.NoStore);
            var email = await Factory.Database.QuerySingleOrDefaultAsync<string>(
                "SELECT email FROM users WHERE id=@Id;", new { Id=user.userId });
            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email!, "Password123!"));
            login.EnsureSuccessStatusCode();
            var session = await client.GetFromJsonAsync<UserSessionDto>("/api/auth/session");
            Assert.Equal(new[] { CataloguePermissions.ReadAdmin }, session!.Permissions);
            using var content = MultipartContentBuilder.CreateIngredientRequest(name: "Revoked session write");
            using var denied = await client.PostAsync("/api/admin/ingredient-details", content);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [Fact]
    public async Task Session_DisabledAccountIs401()
    {
        var (client, user) = await CreateCatalogueClientAsync();
        using (client)
        {
            await Factory.Database.ExecuteAsync("UPDATE users SET is_active=0 WHERE id=@Id;", new { Id=user.userId });
            using var response = await client.GetAsync("/api/auth/session");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }

    [Theory]
    [InlineData(false, false, 401)]
    [InlineData(true, true, 401)]
    [InlineData(true, false, 200)]
    public async Task Session_IgnoresForgedPermissionClaims_RejectsLegacyAndStaleTickets(bool includeVersion, bool stale, int status)
    {
        var (ordinary, user) = await CreateAuthenticatedClientAsync();
        using (ordinary)
        using (var raw = Factory.CreateClient(new() { BaseAddress=new Uri("https://localhost"), AllowAutoRedirect=false }))
        {
            var claims = new List<Claim> {
                new(ClaimTypes.NameIdentifier, user.userId.ToString()), new(ClaimTypes.Name, "synthetic"),
                new(ClaimTypes.Role, "Admin"), new("permission", CataloguePermissions.Write),
                new(AuthSessionPolicy.OriginalIssuedUtcClaim, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) };
            if (includeVersion) claims.Add(new(CataloguePermissions.SecurityVersionClaim, stale ? "1" : "0"));
            var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,
                CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties {
                    IssuedUtc=DateTimeOffset.UtcNow, ExpiresUtc=DateTimeOffset.UtcNow.AddHours(1) },
                CookieAuthenticationDefaults.AuthenticationScheme);
            raw.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
            using var response = await raw.GetAsync("/api/auth/session");
            Assert.Equal((HttpStatusCode)status, response.StatusCode);
            if (status == 200)
                Assert.Empty((await response.Content.ReadFromJsonAsync<UserSessionDto>())!.Permissions);
        }
    }

    [Fact]
    public async Task PermissionLookup_RechecksSecurityVersion_AndDistinguishesEmptyFromInvalidAccount()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        using (client)
        using (var scope = Factory.Services.CreateScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<ICatalogueAuthorizationQueries>();
            Assert.Empty((await queries.GetPermissionsAsync(user.userId, 0, default))!);
            Assert.Null(await queries.GetPermissionsAsync(int.MaxValue, 0, default));
            Assert.Null(await queries.GetPermissionsAsync(user.userId, -1, default));
            var permissionOperator = new PermissionOperator(Factory.Database.ConnectionString);
            Assert.True(await permissionOperator.ChangeAsync(user.userId, CataloguePermissions.Write,
                true, "synthetic-session-test-operator", null, default));
            Assert.Null(await queries.GetPermissionsAsync(user.userId, 0, default));
            Assert.Equal(new[] { CataloguePermissions.Write }, await queries.GetPermissionsAsync(user.userId, 1, default));
            await Factory.Database.ExecuteAsync("UPDATE users SET is_active=0 WHERE id=@Id;", new { Id=user.userId });
            Assert.Null(await queries.GetPermissionsAsync(user.userId, 1, default));
        }
    }

    [Fact]
    public async Task OpenApi_SessionIncludesRequiredStringArray_AndNoSelectorParameters()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schema = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("UserSessionDto");
        var permissions = schema.GetProperty("properties").GetProperty("permissions");
        Assert.Equal("array", permissions.GetProperty("type").GetString());
        Assert.Equal("string", permissions.GetProperty("items").GetProperty("type").GetString());
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), p => p.GetString() == "permissions");
        var operation = json.RootElement.GetProperty("paths").GetProperty("/api/auth/session").GetProperty("get");
        Assert.False(operation.TryGetProperty("parameters", out var parameters) && parameters.GetArrayLength() != 0);
        Assert.EndsWith("/UserSessionDto", operation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
    }
}
