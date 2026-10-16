using Appetee.Api.Tests.Infrastructure;
using Appetee.DatabaseMigrator;

namespace Appetee.Api.Tests.Integration;

public sealed class AccountClosureSchemaRepairTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCleanupTable_IsRepairedWithoutChangingDataOrInventingHistory(bool historyExists)
    {
        await Factory.Database.ExecuteAsync("DROP TABLE account_closure_cleanup;");
        if(historyExists) await Factory.Database.ExecuteAsync("""
            CREATE TABLE appetee_schema_migrations(migration_id VARCHAR(255),checksum_sha256 CHAR(64),sequence_number INT);
            """);
        var before=await SnapshotAsync();
        var output=new StringWriter(); var runner=new MigrationRunner(Factory.Database.ConnectionString,output);
        await runner.RepairAccountClosureAsync();
        await runner.RepairAccountClosureAsync();
        Assert.Equal(before,await SnapshotAsync());
        Assert.Contains("Created account_closure_cleanup",output.ToString()); Assert.Contains("already exists",output.ToString());
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_closure_cleanup;"));
        Assert.Equal(historyExists?1:0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='appetee_schema_migrations';"));
        if(historyExists) Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM appetee_schema_migrations;"));
        await Factory.Database.ExecuteAsync("INSERT INTO account_closure_cleanup(former_user_id) VALUES(1);");
        await runner.RepairAccountClosureAsync();
        Assert.Equal(1,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM account_closure_cleanup WHERE status='pending';"));
    }

    [Fact]
    public async Task TrackedHistory_IsRejectedInsteadOfSilentlyRepairingAppliedMigration()
    {
        await Factory.Database.ExecuteAsync("""
            DROP TABLE account_closure_cleanup;
            CREATE TABLE appetee_schema_migrations(migration_id VARCHAR(255),checksum_sha256 CHAR(64),sequence_number INT);
            INSERT INTO appetee_schema_migrations VALUES('synthetic-applied','synthetic-checksum',7);
            """);
        var before=await SnapshotAsync(); var runner=new MigrationRunner(Factory.Database.ConnectionString,TextWriter.Null);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>runner.RepairAccountClosureAsync());
        Assert.Contains("tracked database",error.Message);
        Assert.Equal(before,await SnapshotAsync());
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='account_closure_cleanup';"));
    }

    [Fact]
    public async Task UnexpectedExistingCleanupTable_IsNotOverwritten()
    {
        await Factory.Database.ExecuteAsync("DROP TABLE account_closure_cleanup; CREATE TABLE account_closure_cleanup(id INT PRIMARY KEY); INSERT INTO account_closure_cleanup VALUES(123);");
        var runner=new MigrationRunner(Factory.Database.ConnectionString,TextWriter.Null);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>runner.RepairAccountClosureAsync());
        Assert.Contains("unexpected shape",error.Message);
        Assert.Equal(123,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT id FROM account_closure_cleanup;"));
    }

    [Fact]
    public async Task UnsupportedLegacyPrerequisites_RefuseRepair()
    {
        await Factory.Database.ExecuteAsync("DROP TABLE account_closure_cleanup; ALTER TABLE recipes DROP COLUMN card_image_blob_name;");
        var runner=new MigrationRunner(Factory.Database.ConnectionString,TextWriter.Null);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>runner.RepairAccountClosureAsync());
        Assert.Contains("prerequisites",error.Message);
        Assert.Equal(0,await Factory.Database.QuerySingleOrDefaultAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='account_closure_cleanup';"));
    }

    private Task<string?> SnapshotAsync()=>Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_OBJECT('users',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,username,created_at,updated_at)) FROM users),
          'recipes',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,name,created_at,updated_at)) FROM recipes),
          'favorites',(SELECT JSON_ARRAYAGG(JSON_ARRAY(user_id,recipe_id)) FROM favorite_recipes),
          'plans',(SELECT JSON_ARRAYAGG(JSON_ARRAY(id,user_id,name,created_at,updated_at)) FROM nutrition_plans)) AS CHAR);
        """);
}
