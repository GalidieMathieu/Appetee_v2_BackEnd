using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Catalogue;
using Appetee.Application.Requests.Auth;
using Appetee.DatabaseMigrator;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Appetee.Api.Tests.Integration;

public sealed class E002CatalogueSecurityTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("/api/admin/ingredient-details/1")]
    [InlineData("/api/admin/recipe-details/1")]
    public async Task Reads_Anonymous401_Ordinary403(string route)
    {
        using var anonymous=await Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        var (client,user)=await CreateAuthenticatedClientAsync();
        using (client)
        {
            using var response=await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_permissions WHERE user_id=@Id;",new { Id=user.userId }));
        }
    }

    [Theory]
    [InlineData("POST","/api/admin/recipe-details")]
    [InlineData("POST","/api/admin/ingredient-details")]
    [InlineData("PUT","/api/admin/recipe-details/1")]
    public async Task Mutations_Ordinary403_ChangeNothing(string method,string route)
    {
        var (client,_)=await CreateAuthenticatedClientAsync();
        using (client)
        using (var request=new HttpRequestMessage(new HttpMethod(method),route) { Content=new MultipartFormDataContent() })
        {
            using var response=await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM administrative_audit;"));
            Assert.Equal(1L,await Factory.Database.QuerySingleOrDefaultAsync<long>("SELECT resource_version FROM recipes WHERE id=1;"));
        }
    }

    [Theory]
    [InlineData("Catalog.ReadAdmin",true,false)]
    [InlineData("Catalog.Write",false,true)]
    public async Task Permissions_AreIndependent(string permission,bool canRead,bool canWrite)
    {
        var (client,user)=await CreateCatalogueClientAsync(permissions:[permission]);
        using (client)
        {
            using var read=await client.GetAsync("/api/admin/ingredient-details/1");
            Assert.Equal(canRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden,read.StatusCode);
            using var content=MultipartContentBuilder.CreateIngredientRequest(name:"Independent permission ingredient");
            using var create=await client.PostAsync("/api/admin/ingredient-details",content);
            Assert.Equal(canWrite ? HttpStatusCode.OK : HttpStatusCode.Forbidden,create.StatusCode);
            Assert.Equal(canWrite ? 1 : 0,await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM administrative_audit WHERE operation='catalogue_created' AND actor_user_id=@Id;",new { Id=user.userId }));
        }
    }

    [Fact]
    public async Task Read_ReturnsVersionETagAndNullLegacyActors_OrdinaryContractUnchanged()
    {
        var (client,_)=await CreateCatalogueClientAsync();
        using (client)
        {
            using var response=await client.GetAsync("/api/admin/recipe-details/1");
            response.EnsureSuccessStatusCode();
            Assert.Equal("\"catalog-recipe-1-v1\"",response.Headers.ETag!.ToString());
            var dto=await response.Content.ReadFromJsonAsync<CatalogueResourceDto<RecipeDetailDto>>();
            Assert.Equal("1",dto!.ResourceVersion);
            Assert.Null(dto.CreatedByUserId); Assert.Null(dto.UpdatedByUserId);
            using var ordinary=await client.GetAsync("/api/recipes/1");
            var text=await ordinary.Content.ReadAsStringAsync();
            Assert.DoesNotContain("catalogueMetadata",text);
            Assert.DoesNotContain("resourceVersion",text);
        }
    }

    [Theory]
    [InlineData(null,428)]
    [InlineData("*",412)]
    [InlineData("W/\"catalog-recipe-1-v1\"",412)]
    [InlineData("\"catalog-recipe-1-v2\"",412)]
    [InlineData("\"catalog-recipe-2-v1\"",412)]
    [InlineData("\"catalog-recipe-1-v01\"",412)]
    [InlineData("\"catalog-recipe-1-v1\",\"catalog-recipe-1-v2\"",412)]
    public async Task Update_RequiresExactPreconditionAndSavesNothing(string? token,int status)
    {
        var (client,_)=await CreateCatalogueClientAsync();
        using (client)
        {
            if (token is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match",token);
            using var content=MultipartContentBuilder.CreateRecipeRequest(name:"Rejected update",includeImage:false);
            using var response=await client.PutAsync("/api/admin/recipe-details/1",content);
            Assert.Equal((HttpStatusCode)status,response.StatusCode);
            var problem=await response.ReadProblemDetailsAsync();
            Assert.Equal(status==428 ? "catalog_precondition_required" : "catalog_precondition_failed",problem!.Extensions["code"]?.ToString());
            Assert.Equal(1L,await Factory.Database.QuerySingleOrDefaultAsync<long>("SELECT resource_version FROM recipes WHERE id=1;"));
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM administrative_audit WHERE operation='catalogue_updated';"));
        }
    }

    [Fact]
    public async Task ConcurrentUpdates_OneCommitsOne412_ActorAndAuditAtomic_MealRevisionUnchanged()
    {
        var (client,user)=await CreateCatalogueClientAsync();
        using (client)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match","\"catalog-recipe-1-v1\"");
            using var first=MultipartContentBuilder.CreateRecipeRequest(name:"Concurrent first",includeImage:false);
            using var second=MultipartContentBuilder.CreateRecipeRequest(name:"Concurrent second",includeImage:false);
            var responses=await Task.WhenAll(client.PutAsync("/api/admin/recipe-details/1",first),client.PutAsync("/api/admin/recipe-details/1",second));
            try
            {
                Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);
                Assert.Single(responses,r=>(int)r.StatusCode==412);
                Assert.Equal("\"catalog-recipe-1-v2\"",responses.Single(r=>r.IsSuccessStatusCode).Headers.ETag!.ToString());
                Assert.Equal(user.userId,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT updated_by_user_id FROM recipes WHERE id=1;"));
                Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM administrative_audit WHERE operation='catalogue_updated' AND resource_version=2;"));
                Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM meal_plan_user_settings;"));
            }
            finally { foreach(var response in responses)response.Dispose(); }
        }
    }

    [Fact]
    public async Task PermissionChanges_RevokeOldTicketsAndRecordOperator_NoOpDoesNotAdvanceVersion()
    {
        var (client,user)=await CreateCatalogueClientAsync();
        using (client)
        {
            var op=new PermissionOperator(Factory.Database.ConnectionString);
            Assert.False(await op.ChangeAsync(user.userId,"Catalog.Write",true,"test-security-operator",null,default));
            using var unchanged=await client.GetAsync("/api/admin/recipe-details/1");
            Assert.Equal(HttpStatusCode.OK,unchanged.StatusCode);
            Assert.True(await op.ChangeAsync(user.userId,"Catalog.Write",false,"test-security-operator",null,default));
            using var stale=await client.GetAsync("/api/admin/recipe-details/1");
            Assert.Equal(HttpStatusCode.Unauthorized,stale.StatusCode);
            var email=await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT email FROM users WHERE id=@Id;",new{Id=user.userId});
            using var login=await client.PostAsJsonAsync("/api/auth/login",new LoginRequest(email!,"Password123!"));login.EnsureSuccessStatusCode();
            using var retainedRead=await client.GetAsync("/api/admin/recipe-details/1");retainedRead.EnsureSuccessStatusCode();
            using var content=MultipartContentBuilder.CreateIngredientRequest(name:"Rejected revoked writer");
            using var deniedWrite=await client.PostAsync("/api/admin/ingredient-details",content);
            Assert.Equal(HttpStatusCode.Forbidden,deniedWrite.StatusCode);
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM administrative_audit WHERE operation='permission_revoked' AND actor_reference='test-security-operator' AND occurred_at IS NOT NULL;"));
        }
    }

    [Fact]
    public async Task DisabledAccount_CannotUseExistingAdminTicket()
    {
        var (client,user)=await CreateCatalogueClientAsync();
        using (client)
        {
            await Factory.Database.ExecuteAsync("UPDATE users SET is_active=0 WHERE id=@Id;",new { Id=user.userId });
            using var response=await client.GetAsync("/api/admin/recipe-details/1");
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        }
    }

    [Theory]
    [InlineData(false,false,401)]
    [InlineData(true,false,403)]
    [InlineData(true,true,401)]
    public async Task LegacyForgedAndStaleClaims_DoNotGrantAuthority(bool includeVersion,bool stale,int expectedStatus)
    {
        var (ordinary,user)=await CreateAuthenticatedClientAsync();
        using (ordinary)
        using (var raw=Factory.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false}))
        {
            var claims=new List<Claim> {
                new(ClaimTypes.NameIdentifier,user.userId.ToString()), new(ClaimTypes.Name,"synthetic"),
                new(ClaimTypes.Role,"Admin"),new("permission","Catalog.ReadAdmin"),
                new(Appetee.Infrastructure.Auth.AuthSessionPolicy.OriginalIssuedUtcClaim,DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) };
            if(includeVersion)claims.Add(new(CataloguePermissions.SecurityVersionClaim,stale ? "1" : "0"));
            var options=Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
            var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IssuedUtc=DateTimeOffset.UtcNow,ExpiresUtc=DateTimeOffset.UtcNow.AddHours(1) },CookieAuthenticationDefaults.AuthenticationScheme);
            raw.DefaultRequestHeaders.TryAddWithoutValidation("Cookie",options.Cookie.Name+"="+options.TicketDataFormat.Protect(ticket));
            using var response=await raw.GetAsync("/api/admin/recipe-details/1");
            Assert.Equal((HttpStatusCode)expectedStatus,response.StatusCode);
        }
    }

    [Fact]
    public async Task AdditiveMigration_PreservesRowsAndGrantsNobody()
    {
        // Downgrade only the guarded synthetic fixture, then exercise the actual forward migration.
        var before=await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT GROUP_CONCAT(CONCAT(id,':',name) ORDER BY id) FROM recipes;");
        await Factory.Database.ExecuteAsync("""
            DROP TABLE account_permissions; DROP TABLE administrative_audit;
            ALTER TABLE users DROP CHECK chk_users_security_version,DROP COLUMN is_active,DROP COLUMN security_version;
            ALTER TABLE recipes DROP CHECK chk_recipes_resource_version,DROP COLUMN resource_version,DROP COLUMN created_by_user_id,DROP COLUMN updated_by_user_id;
            ALTER TABLE ingredients DROP CHECK chk_ingredients_resource_version,DROP COLUMN resource_version,DROP COLUMN created_by_user_id,DROP COLUMN updated_by_user_id;
            """);
        await Factory.Database.ExecuteMigrationAsync("migrations/20261012_010_e002_catalogue_permissions.sql");
        Assert.Equal(before,await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT GROUP_CONCAT(CONCAT(id,':',name) ORDER BY id) FROM recipes;"));
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_permissions;"));
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipes WHERE resource_version<>1 OR created_by_user_id IS NOT NULL OR updated_by_user_id IS NOT NULL;"));
    }

    [Fact]
    public async Task MissingPrecondition_Is428BeforeMultipartBinding_AndNoStore()
    {
        var (client,_)=await CreateCatalogueClientAsync();
        using(client)
        using(var request=new HttpRequestMessage(HttpMethod.Put,"/api/admin/recipe-details/1"))
        {
            request.Content=new StringContent("malformed body that must not execute");
            using var response=await client.SendAsync(request);
            Assert.Equal((HttpStatusCode)428,response.StatusCode);Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Equal(1L,await Factory.Database.QuerySingleOrDefaultAsync<long>("SELECT resource_version FROM recipes WHERE id=1;"));
        }
    }

    [Fact]
    public async Task AccountClosure_CascadesPermissionMembershipButPreservesCatalogueAuditAndActors()
    {
        var (client,user)=await CreateCatalogueClientAsync();
        using(client)
        {
            using var content=MultipartContentBuilder.CreateIngredientRequest(name:"Retained authored ingredient");
            using var created=await client.PostAsync("/api/admin/ingredient-details",content);created.EnsureSuccessStatusCode();
            var ingredient=(await created.Content.ReadFromJsonAsync<CatalogueResourceDto<IngredientAdminDetailDto>>())!;
            using var closed=await client.DeleteAsync("/api/users/me");closed.EnsureSuccessStatusCode();
            Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_permissions WHERE user_id=@Id;",new{Id=user.userId}));
            Assert.Equal(user.userId,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT created_by_user_id FROM ingredients WHERE id=@Id;",new{Id=ingredient.Resource.Id}));
            Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM administrative_audit WHERE actor_user_id=@Id AND operation='catalogue_created';",new{Id=user.userId}));
        }
    }

    [Fact]
    public async Task AuditFailure_RollsBackResourceVersionActorsAndRelationships()
    {
        var (client,_)=await CreateCatalogueClientAsync();
        using (client)
        {
            var before=await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT CONCAT(name,':',resource_version,':',IFNULL(updated_by_user_id,'null')) FROM recipes WHERE id=1;");
            await Factory.Database.ExecuteAsync("CREATE TRIGGER e002_fail_audit BEFORE INSERT ON administrative_audit FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='synthetic audit fault';");
            try
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match","\"catalog-recipe-1-v1\"");
                using var content=MultipartContentBuilder.CreateRecipeRequest(name:"Must roll back",includeImage:false);
                using var response=await client.PutAsync("/api/admin/recipe-details/1",content);
                Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
                Assert.Equal(before,await Factory.Database.QuerySingleOrDefaultAsync<string>("SELECT CONCAT(name,':',resource_version,':',IFNULL(updated_by_user_id,'null')) FROM recipes WHERE id=1;"));
                Assert.Equal(3,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM recipe_ingredients WHERE recipe_id=1;"));
            }
            finally { await Factory.Database.ExecuteAsync("DROP TRIGGER IF EXISTS e002_fail_audit;"); }
        }
    }
}
