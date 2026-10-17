# F-011 MySQL 8.4 alias compatibility

2026-10-16. Backend-only source fix; no database upgrade, schema/data migration, server configuration change or deployment was performed.

## Incident and fix

Production application events for `GET /api/meal-plan/days` preserved MySqlConnector error 1064 and pointed into the four-result planner reader. The user reported Azure MySQL 8.4. The item query used `manual` as an unquoted alias for `meal_plan_manual_items`. MySQL introduced `MANUAL` as a reserved word in 8.4.0; the current reference notes that it becomes nonreserved in 8.4.11. This explains the query's incompatibility with affected 8.4 releases; the exact deployed Azure patch and post-fix outcome still need verification.

The fix renames only the table alias to `manual_item` in `MealPlannerSql.Items` and all six projected manual-detail column references. Table/column names, projection names, recipe compatibility, ownership/date filters, ordering, four-result read shape, write/revision behavior and API contracts are unchanged. No migration is required for this alias change.

References: [MySQL 8.4 keywords](https://dev.mysql.com/doc/refman/8.4/en/keywords.html), [provider issue describing the original reserved words and incorrect metadata flags](https://bugs.mysql.com/bug.php?id=114874). On affected versions, `INFORMATION_SCHEMA.KEYWORDS.RESERVED` alone can be misleading because that provider issue reports incorrect flags.

## Verification actually performed

```powershell
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MealPlannerSqlCompatibilityTests" --nologo --logger "console;verbosity=minimal"
dotnet build Appetee.sln --configuration Release --no-restore --nologo
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~Appetee.Api.Tests.Unit" --nologo --logger "console;verbosity=minimal"
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~Appetee.Api.Tests.Diagnostics" --nologo --logger "console;verbosity=minimal"
```

- Before the source fix: new regression checks failed as expected (7 failed, 1 passed).
- After the fix: Release solution build passed with zero warnings/errors; all 343 Unit tests and the 1 isolated Diagnostics smoke passed, zero skipped. The eight new checks cover the nonreserved alias, every manual projection name and unchanged four-result/filter/order contract.
- These are source-level regression checks, not a MySQL 8.4 parser/integration run. No live MySQL integration/reset tests were run; that existing suite can rebuild its dedicated test database and requires separate opt-in.
- Deployment and replaying a harmless planner GET against Azure remain pending. Confirm a successful response and no 1064 under the new release before declaring the production incident resolved. Do not blindly replay mutations.
- No frontend files changed. No application/antiforgery cookies or other secret values are copied into this record. IIS/platform cookie capture remains a separate operational security issue from the application privacy boundary.

## Matching local development to Azure

Read-only machine inspection found a running `MySQL80` service and an installed `MySQL Server 8.0` binary reporting 8.0.35. No database connection, backup or upgrade was attempted. Docker is installed, but its daemon/configuration was not verified because access was denied; no container was started. Workbench's product version is not the server version.

Recommended low-risk approach: a side-by-side 8.4 server with its own Windows service, fresh data directory and unused port (for example, 3307, after checking availability). Keep the existing 8.0 service/data as a fallback until the new instance passes validation. Do not point two services at the same data directory or reuse the old directory as an unreviewed upgrade shortcut.

1. Get the exact Azure patch and relevant non-secret settings using a read-only query. Match the exact supported 8.4 patch for parity, not just the moving latest 8.4 release.

   ```sql
   SELECT VERSION() AS mysql_version, @@sql_mode AS sql_mode,
          @@character_set_server AS character_set_server,
          @@collation_server AS collation_server,
          @@lower_case_table_names AS lower_case_table_names;
   ```

2. Run MySQL Shell's upgrade checker for the chosen target and review findings. Make a protected logical backup of the required application databases, including their routines/triggers/events as applicable, and verify that it restores. Do not commit dumps or credentials to source control. Do not blindly import 8.0 system schemas into the new server.
3. Install/configure the separate MySQL 8.4 instance through the Windows MSI/MySQL Configurator or an explicitly approved container setup. Restore the application backup into the fresh target and configure a dedicated local application identity. Installation, restore, account creation and any in-place upgrade require separate approval; this document performs none of them.
4. Point the backend's local secret configuration and a separate Workbench connection at the new port. Maintain a distinct, disposable integration-test database with explicit test opt-in; never point reset tests at developer or production data. Validate schema/migrations, counts, recipe/manual planner reads, writes, auth and constraints before switching routine development.
5. Match SQL mode, charset/collation, schema/migrations and relevant server settings as well as version. Windows and Azure can differ in table-name case behavior; version equality alone is not full environment parity. Do not change initialization-sensitive settings on an existing data directory without a reviewed plan.

Only after successful validation and explicit approval should the old service/data be retired. An in-place upgrade is also a supported class of procedure, but is not being chosen or executed here; follow the provider's exact source/target prerequisites and backup/rollback process rather than assuming it is reversible.

Provider guides: [upgrade prerequisites/checker](https://dev.mysql.com/doc/refman/8.4/en/upgrade-prerequisites.html), [Windows installation/upgrade](https://dev.mysql.com/doc/refman/8.4/en/windows-upgrading.html), [separate Windows instances](https://dev.mysql.com/doc/refman/8.4/en/multiple-windows-servers.html), [dump/load compatibility validation](https://dev.mysql.com/doc/refman/8.4/en/mysqldump-upgrade-testing.html).
