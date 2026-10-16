# Database migrations

Production schema changes are applied only by `Appetee.DatabaseMigrator`. The API never migrates on startup.

Use a dedicated least-privileged migration identity. For local development, edit
`src/Appetee.DatabaseMigrator/appsettings.migrations.json` (created with an empty connection
string). On a fresh checkout, create that file from the adjacent
`appsettings.migrations.example.json` template. Set your real local credentials:

```json
{
  "ConnectionStrings": {
    "AppeteeMigrationDb": "Server=localhost;Port=3306;Database=appetee;User ID=YOUR_MIGRATION_USER;Password=YOUR_PASSWORD;"
  }
}
```

This is the database to migrate, not a second database. Change the host/database
and credentials here when you want a different target. The file is Git-ignored,
copied to build output, and deliberately excluded from publish output to avoid
shipping local credentials. JSON is read relative to the executable directory,
not the shell's working directory. `dotnet run` builds/copies the edited settings;
when using `--no-build`, update the output settings yourself or rebuild first.
The API's appsettings files are never loaded by the migrator.

From the backend root, no connection-string environment variable is required:

```powershell
dotnet run --project src\Appetee.DatabaseMigrator -- migrate
```

For a published migrator, provision an `appsettings.migrations.json` next to its executable
with the intended deployment connection. Existing environment-only deployment
using `ConnectionStrings__AppeteeMigrationDb` remains supported **only when that
JSON file is absent**. A present blank/invalid file fails instead of silently
using an environment value or connecting to another database. JSON takes precedence.
Do not check real credentials into Git or reuse a local configuration in production.

The runner requires MySQL 8.0.16 or later, takes a database-scoped advisory lock, validates immutable SHA-256 checksums, and records successful work in `appetee_schema_migrations`. MySQL DDL can commit implicitly, so failed deployments are recovered by fixing forward and rerunning the restartable migration; they are not described as transaction rollbacks.

For an existing database created before migration history was introduced, first verify its schema and backup/PITR point. Then explicitly record the audited baseline through migration `007`:

Connection settings still come from JSON. The baseline environment flag below
is a separate deliberate safety opt-in, not a credential setting; it remains
required for this one-time adoption operation.

```powershell
$env:APPETEE_ALLOW_MIGRATION_BASELINE = 'true'
dotnet run --project src\Appetee.DatabaseMigrator -- baseline --through 7
Remove-Item Env:\APPETEE_ALLOW_MIGRATION_BASELINE
dotnet run --project src\Appetee.DatabaseMigrator -- migrate
```

Baselining does not execute historical SQL and is rejected when the expected schema signature is absent or history already exists. An empty database is created from the embedded canonical generated schema and all migrations in that build are recorded as baseline-applied.

`scriptDatabase.sql`, `initObjectDatabase.sql`, and `data/tools` reset commands are destructive local/test bootstrap tools. They are not production migration commands.

## Missing legacy account-closure table

If an existing database has no applied migration history and startup reports
`Table 'appetee.account_closure_cleanup' doesn't exist`, do not disable the worker,
rebuild the database, or falsely baseline migration 007. After verifying the
configured target and its schema, the narrow repair command is:

```powershell
dotnet run --project src\Appetee.DatabaseMigrator -- repair-account-closure
```

It uses the existing immutable migration 007 to create only the missing cleanup
table. It validates MySQL/schema prerequisites, holds the migration advisory lock,
rejects nonempty migration history, and leaves an existing complete-column table
and all its rows unchanged. An unexpected existing table shape is rejected rather
than overwritten. Column-presence checking is not a full schema-adoption audit.
It does not write migration history: recording only 007 could make a later
`migrate` replay earlier SQL that already ran. Auditing and explicitly adopting
the actual legacy schema version remain separate steps. MySQL DDL can commit
implicitly; this is an additive table repair, not a transactional rebuild.

### Verified local repair (2026-10-11)

The startup error was reproduced as a missing table on verified
`localhost:3306/appetee`, MySQL 8.0.35. The meal-plan tables were already present,
and `appetee_schema_migrations` existed with zero rows. The API and migration JSON
targeted the same local database. No credentials were logged or changed.

Changed `src/Appetee.DatabaseMigrator/Program.cs`,
`src/Appetee.DatabaseMigrator/MigrationRunner.cs`, this guide, and added
`tests/Appetee.Api.Tests/Integration/AccountClosureSchemaRepairTests.cs`.
Historical SQL, canonical generated schema, API worker and unrelated work were
preserved: the canonical schema already includes the cleanup table.

Verification commands:

```powershell
dotnet build Appetee.sln --no-restore
# The following integration run requires the guarded isolated database opt-in:
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~AccountClosure|FullyQualifiedName~F011B01MigrationRunnerTests' --logger 'console;verbosity=normal'
# Actual repair on the previously verified local application database:
dotnet src/Appetee.DatabaseMigrator/bin/Debug/net10.0/Appetee.DatabaseMigrator.dll repair-account-closure
```

Build passed (zero errors; 15 existing ImageSharp NU1902/NU1903 advisory warnings).
Targeted tests passed 13/13, zero failed/skipped, time 52.9287 seconds: five guarded
repair cases, five existing account-closure cases, three migration/parser unit
cases. Test table drops/alterations occurred only in loopback
`appetee_backend_tests` after explicit opt-in. These tests did not rebuild the
local application database.

The actual local action created only `account_closure_cleanup` using migration
007. Read-only checks before/after showed unchanged counts: 3 users, 1699 recipes,
16 favorites, and zero plans/days/meals/items. The original worker SELECT then
succeeded with zero due cleanup rows. Migration history remained empty.
No production target, application reset script, account deletion, or historical
baseline was invoked. The ImageSharp vulnerabilities remain a separate issue;
no package upgrade or warning suppression was made in this schema fix.

## JSON settings verification (2026-10-11)

Implemented in `MigrationSettings.cs` and wired through the migrator's `Program.cs`.
The migrator project copies the distinctly named JSON for local execution; API
helpers, migration SQL, generated schema, and API settings were not changed.
The local private file is Git-ignored and initialized with an empty value; no
credentials were copied or guessed.

Commands actually run from the backend root:

```powershell
dotnet build Appetee.sln --no-restore -t:Rebuild
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~MigrationSettingsTests|FullyQualifiedName~F011B01MigrationRunnerTests' --logger 'console;verbosity=normal'
dotnet publish src/Appetee.DatabaseMigrator/Appetee.DatabaseMigrator.csproj -c Debug --no-build --no-restore -o .dotnet/migrator-json-publish-check
```

Build passed (0 errors, 3 existing NU1900 vulnerability-feed warnings); 14/14 unit
cases passed, zero failed/skipped. Publish passed; actual output excluded private
JSON and included the non-secret template. After rebuild, test-output API
appsettings matched the API source file; migration settings use a distinct name
to avoid a copied-content collision between referenced projects. Coverage includes
JSON selection, invalid/blank settings failing closed, environment-only legacy
fallback when JSON is absent, safe malformed-JSON errors, comments/trailing commas,
and existing embedded migration/parser checks. No database connection, migration,
baseline operation, or destructive database reset was run for this change.
