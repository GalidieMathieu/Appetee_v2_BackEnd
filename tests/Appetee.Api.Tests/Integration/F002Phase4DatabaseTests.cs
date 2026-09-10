/*
 * Purpose: Verifies the Phase 4 forward migration upgrades the legacy recovery-token indexes safely.
 * Created: 2026-08-23T00:23:05-06:00
 * Last updated: 2026-08-23T00:23:05-06:00
 */

using Appetee.Api.Tests.Infrastructure;

namespace Appetee.Api.Tests.Integration;

public sealed class F002Phase4DatabaseTests(
    AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Migration_AddsUniqueTokenLookupAndUserCooldownIndex()
    {
        await Factory.Database.ExecuteAsync("""
            ALTER TABLE password_reset_tokens
                DROP KEY uq_prt_token_hash,
                DROP KEY idx_prt_user_created_at,
                ADD KEY idx_prt_user_id (user_id);
            """);

        await Factory.Database.ExecuteMigrationAsync(
            "migrations/20260823_005_f002_phase4_password_recovery.sql");

        var uniqueTokenColumns = await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = 'password_reset_tokens'
              AND index_name = 'uq_prt_token_hash'
              AND non_unique = 0
              AND column_name = 'token_hash';
            """);
        var cooldownIndexColumns = await Factory.Database.QuerySingleOrDefaultAsync<int>("""
            SELECT COUNT(*)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = 'password_reset_tokens'
              AND index_name = 'idx_prt_user_created_at'
              AND column_name IN ('user_id', 'created_at');
            """);

        Assert.Equal(1, uniqueTokenColumns);
        Assert.Equal(2, cooldownIndexColumns);
    }
}
