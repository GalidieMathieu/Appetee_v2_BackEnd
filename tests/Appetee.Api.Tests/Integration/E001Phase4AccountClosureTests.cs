/*
 * Purpose: Verifies E-001 Phase 4 relational deletion, session revocation, and retryable media cleanup.
 * Change reason: Add the account-closure integration and fault test matrix required by the initiative.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Abstractions.Users;
using Appetee.Application.Requests;
using Appetee.Application.Requests.Auth;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class E001Phase4AccountClosureTests(
    AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Factory.BlobStorage.Reset();
    }

    [Fact]
    public async Task DeleteMe_CascadesOwnedDataDeletesManagedMediaAndRevokesAllCookies()
    {
        const string email = "closure@appetee.test";
        const string password = "Password123!";
        var (firstBrowser, account) = await CreateAuthenticatedClientAsync(
            username: "closure_user",
            email: email,
            password: password);
        using var ownerClient = firstBrowser;
        using var otherBrowser = CreateClient();
        var blobName = $"users/{account.userId}/account-closure.avif";

        using var loginResponse = await otherBrowser.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password, RememberMe: true));
        loginResponse.EnsureSuccessStatusCode();

        var imageUrl = await Factory.BlobStorage.UploadAsync(
            blobName,
            new MemoryStream([1, 2, 3]),
            "image/avif");
        using var profileResponse = await ownerClient.PutAsJsonAsync(
            "/api/users/me",
            new UpdateCurrentUserProfileRequest(null, imageUrl));
        profileResponse.EnsureSuccessStatusCode();

        var collectionId = await SeedOwnedRowsAsync(account.userId);

        using var deleteResponse = await ownerClient.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(deleteResponse.Headers.Contains("Set-Cookie"));
        Assert.False(Factory.BlobStorage.Contains(blobName));
        Assert.Equal(0, await CountAccountAndOwnedRowsAsync(account.userId));
        Assert.Equal(
            0,
            await Factory.Database.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM favorite_collection_recipes WHERE collection_id = @CollectionId;",
                new { CollectionId = collectionId }));

        using var firstSession = await ownerClient.GetAsync("/api/auth/session");
        using var otherSession = await otherBrowser.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, firstSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherSession.StatusCode);

        var cleanup = await GetLatestCleanupAsync();
        Assert.NotNull(cleanup);
        Assert.Equal("completed", cleanup!.Status);
        Assert.Equal(0u, cleanup.AttemptCount);
        Assert.Null(cleanup.FormerUserId);
        Assert.Null(cleanup.ProfileImageUrl);
    }

    [Fact]
    public async Task DeleteMe_WhenBlobFails_CommitsDeletionAndRetryReconcilesCleanup()
    {
        var (client, account) = await CreateAuthenticatedClientAsync();
        using var ownerClient = client;
        var blobName = $"users/{account.userId}/retry-account-closure.avif";
        var imageUrl = await Factory.BlobStorage.UploadAsync(
            blobName,
            new MemoryStream([4, 5, 6]),
            "image/avif");
        using var profileResponse = await ownerClient.PutAsJsonAsync(
            "/api/users/me",
            new UpdateCurrentUserProfileRequest(null, imageUrl));
        profileResponse.EnsureSuccessStatusCode();
        Factory.BlobStorage.FailNextDeleteAttempts(1);

        using var deleteResponse = await ownerClient.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(0, await CountAccountAndOwnedRowsAsync(account.userId));
        Assert.True(Factory.BlobStorage.Contains(blobName));

        var pending = await GetLatestCleanupAsync();
        Assert.NotNull(pending);
        Assert.Equal("pending", pending!.Status);
        Assert.Equal(1u, pending.AttemptCount);
        Assert.Equal(account.userId, pending.FormerUserId);
        Assert.Equal(imageUrl, pending.ProfileImageUrl);

        using var scope = Factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IAccountClosureProcessor>();
        Assert.True(await processor.TryProcessAsync(pending.Id, CancellationToken.None));

        var completed = await GetLatestCleanupAsync();
        Assert.NotNull(completed);
        Assert.Equal("completed", completed!.Status);
        Assert.Equal(1u, completed.AttemptCount);
        Assert.Null(completed.FormerUserId);
        Assert.Null(completed.ProfileImageUrl);
        Assert.False(Factory.BlobStorage.Contains(blobName));
    }

    [Fact]
    public async Task DeleteMe_WithExternalProfileUrl_DoesNotTreatItAsManagedMedia()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using var ownerClient = client;
        using var profileResponse = await ownerClient.PutAsJsonAsync(
            "/api/users/me",
            new UpdateCurrentUserProfileRequest(
                null,
                "https://images.example.test/profile.avif"));
        profileResponse.EnsureSuccessStatusCode();
        Factory.BlobStorage.FailNextDeleteAttempts(1);

        using var deleteResponse = await ownerClient.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var cleanup = await GetLatestCleanupAsync();
        Assert.NotNull(cleanup);
        Assert.Equal("completed", cleanup!.Status);
        Assert.Equal(0u, cleanup.AttemptCount);
        Assert.Null(cleanup.FormerUserId);
        Assert.Null(cleanup.ProfileImageUrl);
    }

    [Fact]
    public async Task DeleteMe_WithAnotherAccountsManagedUrl_DoesNotDeleteThatBlob()
    {
        var (client, account) = await CreateAuthenticatedClientAsync();
        using var ownerClient = client;
        var otherAccountBlob = $"users/{account.userId + 1}/profile.avif";
        var imageUrl = await Factory.BlobStorage.UploadAsync(
            otherAccountBlob,
            new MemoryStream([7, 8, 9]),
            "image/avif");
        using var profileResponse = await ownerClient.PutAsJsonAsync(
            "/api/users/me",
            new UpdateCurrentUserProfileRequest(null, imageUrl));
        profileResponse.EnsureSuccessStatusCode();
        Factory.BlobStorage.FailNextDeleteAttempts(1);

        using var deleteResponse = await ownerClient.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(Factory.BlobStorage.Contains(otherAccountBlob));
        var cleanup = await GetLatestCleanupAsync();
        Assert.NotNull(cleanup);
        Assert.Equal("completed", cleanup!.Status);
        Assert.Equal(0u, cleanup.AttemptCount);
        Assert.Null(cleanup.FormerUserId);
        Assert.Null(cleanup.ProfileImageUrl);
    }

    [Fact]
    public async Task ForwardMigration_CreatesPrivacyMinimizedCleanupOutbox()
    {
        await Factory.Database.ExecuteAsync("DROP TABLE account_closure_cleanup;");

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260911_007_e001_phase4_account_closure.sql");

        var columns = await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'account_closure_cleanup';
            """);
        var unnecessaryIdentifyingColumns = await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'account_closure_cleanup'
              AND column_name IN ('email', 'username');
            """);
        var formerUserIdIsNullable = await Factory.Database.QuerySingleOrDefaultAsync<string>("""
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'account_closure_cleanup'
              AND column_name = 'former_user_id';
            """);
        var dueIndexColumns = await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = 'account_closure_cleanup'
              AND index_name = 'idx_account_closure_cleanup_due';
            """);

        Assert.Equal(10, columns);
        Assert.Equal(0, unnecessaryIdentifyingColumns);
        Assert.Equal("YES", formerUserIdIsNullable);
        Assert.Equal(3, dueIndexColumns);
    }

    // These rows cover every current foreign-key path owned by a user, including nested collection entries.
    private async Task<long> SeedOwnedRowsAsync(int userId)
    {
        await Factory.Database.ExecuteAsync("""
            INSERT INTO favorite_recipes (user_id, recipe_id)
            VALUES (@UserId, 1);

            INSERT INTO favorite_collections (user_id, name)
            VALUES (@UserId, 'Closure collection');

            INSERT INTO favorite_collection_recipes (collection_id, recipe_id)
            VALUES (LAST_INSERT_ID(), 1);

            INSERT INTO password_reset_tokens (
                user_id,
                token_hash,
                expires_at,
                created_at
            )
            VALUES (
                @UserId,
                CONCAT('closure-token-', @UserId),
                DATE_ADD(UTC_TIMESTAMP(), INTERVAL 1 HOUR),
                UTC_TIMESTAMP()
            );
            """,
            new { UserId = userId });

        return await Factory.Database.QuerySingleOrDefaultAsync<long>("""
            SELECT id
            FROM favorite_collections
            WHERE user_id = @UserId
              AND name = 'Closure collection'
            LIMIT 1;
            """,
            new { UserId = userId });
    }

    private Task<int> CountAccountAndOwnedRowsAsync(int userId) =>
        Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT
                (SELECT COUNT(*) FROM users WHERE id = @UserId)
              + (SELECT COUNT(*) FROM user_diets WHERE user_id = @UserId)
              + (SELECT COUNT(*) FROM user_ingredient_restrictions WHERE user_id = @UserId)
              + (SELECT COUNT(*) FROM favorite_recipes WHERE user_id = @UserId)
              + (SELECT COUNT(*) FROM favorite_collections WHERE user_id = @UserId)
              + (SELECT COUNT(*) FROM password_reset_tokens WHERE user_id = @UserId);
            """,
            new { UserId = userId });

    private Task<CleanupRow?> GetLatestCleanupAsync() =>
        Factory.Database.QuerySingleOrDefaultAsync<CleanupRow>("""
            SELECT
                id AS Id,
                status AS Status,
                attempt_count AS AttemptCount,
                former_user_id AS FormerUserId,
                profile_image_url AS ProfileImageUrl
            FROM account_closure_cleanup
            ORDER BY id DESC
            LIMIT 1;
            """);

    private sealed record CleanupRow(
        long Id,
        string Status,
        uint AttemptCount,
        int? FormerUserId,
        string? ProfileImageUrl);
}
