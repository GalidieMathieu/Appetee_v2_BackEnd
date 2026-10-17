# Schema migrations and partial-schema repair

## Which SQL is for what?

`scriptDatabase.sql` is the authoritative current table/column/constraint schema.
`data/generated/sql/01-schema.sql` is generated from it without schema overrides.
Forward repair definitions must agree with that source, not introduce another schema.

| Files | Purpose | Existing/production database? |
| --- | --- | --- |
| `migrations/*_NNN_*.sql` | Versioned forward changes, embedded in the migrator | Use the migration process; never replay already recorded scripts |
| `scriptDatabase.sql`, `data/generated/sql/01-schema.sql` | Destructive local/test baseline | **No**: drops application tables and selects `appetee` |
| `data/generated/sql/02-04*.sql` | Generated canonical development seed | Not a production import/upsert workflow |
| `initObjectDatabase.sql` | Destructive, small synthetic endpoint-test fixtures | **No**: not the canonical dataset |

Database rows hold recipes, nutrition, costs, users, plans, relationships and Blob
names. Blob Storage holds image bytes; it is not a backup of relational data.

Historical migrations 001-010 are immutable. Do not modify their checksums or
insert invented history rows to make a partial database appear current.

## 011: missing application tables

`20261016_011_repair_missing_application_tables.sql` is a narrow forward repair
for the observed partial 007-010 schema. It creates only absent:

- `meal_plan_manual_items` (required even for an untouched planner week);
- `account_permissions` (starts empty: no automatic admin grants);
- `account_closure_cleanup` (starts empty: no reconstructed/deleted cleanup jobs).

Definitions match the canonical fresh-database baseline. Existing rows are never
updated/deleted. No catalogue seed, password, token, image, grant/revocation,
database switch, or history adoption is included.

Preflight checks require selected non-system database, parent/application tables,
profile/security columns, InnoDB target tables, required column types/nullability
and named enforced check/FK constraints and primary keys. A view or case-mismatched target fails.
These are compatibility checks, not a complete historical schema/data audit:
review existing defaults, keys, indexes and constraint expressions against the
baseline when adopting an untracked database. Incompatible tables are rejected,
not silently rebuilt.

Preflight and CREATE statements execute inside one stored procedure. Continuing
a SQL-editor script after a failed CALL cannot execute application DDL outside
the guard. MySQL DDL commits implicitly: this is restartable, **not transactional**.
A failure can leave some newly created tables; resolve its cause and rerun.
No script can restore rows lost before this repair.

### Tracked databases

With a verified 001-010 history and matching checksums, deploy the current
migrator and run `migrate` using a dedicated migration identity/configuration.
It holds its existing database advisory lock, applies 011, then records success.
Runtime API credentials must not gain schema/permission-operator powers.
Do not run a separate manual repair concurrently with the migrator.

### Untracked partial Azure database (observed case)

1. Take a verified backup/snapshot and review the target server/database and
   prerequisites. Pause competing deployments/schema changes.
2. In a trusted SQL client connected to the intended Azure server, select
   `appeteedatabase` explicitly. Confirm with `SELECT DATABASE(), VERSION();`.
   Do not copy credentials, connection strings, account rows or raw query
   payloads into tickets/logs.
3. Open and execute **only** `20261016_011_repair_missing_application_tables.sql`
   in full, using an operator identity authorized for routine/table DDL.
   Workbench/CLI must honor `DELIMITER`; API runtime execution is not intended.
   Stop on any error. Do not replace `USE appetee` in a reset script and run it.
4. Confirm the three tables with `SHOW CREATE TABLE`, their constraints and the
   expected empty contents if newly created. Retry the authenticated
   `GET /api/meal-plan/days?from=2026-10-12&through=2026-10-18`; compare the
   response and safe trace-correlated diagnostics, not secrets.
5. Verify account permissions remain unchanged; do not grant access as part of
   this repair. Check normal authentication and account-closure behavior.
6. Perform a separate audited schema/history adoption against **all** earlier
   migrations before using normal automated migration/readiness gates.
   This repair deliberately neither creates `appetee_schema_migrations` nor
   marks 001-011 as applied. Do not use `baseline --through 11` merely because
   the three tables now exist.

Required operational approvals, deployment and Azure/browser verification remain
operator tasks. A successful local test is not evidence of production repair.

## Dataset maintenance without a reset

From `data/tools`:

```powershell
npm test
npm run validate
npm run build
npm run check:generated
npm run db:verify:local
```

Only `db:verify:local` opens MySQL, with SELECT-only checks and the existing exact
loopback/`appetee` target guard. `validate` and `build` write reproducible files;
`check:generated` writes nothing. None uploads Blobs or changes database rows.
Do not run `db:reset:local` on an application database you want to preserve.

The 121 ingredients, 1,699 recipes and 3,519 AVIF assets retain their canonical
nutrition, historical price/provenance and private/test-only media approvals.
Regeneration does not refresh market prices or approve media for public use.

## Verification record

Verified locally on 2026-10-16:

| Command / check | Actual result |
| --- | --- |
| `npm test` (data/tools) | 52 passed, 0 failed |
| `npm run validate` | 121 ingredients, 1,699 recipes; 0 errors; 2,787 existing warnings |
| `npm run build` | All four SQL files, both indexes and reports regenerated from source |
| `npm run check:generated` | All generated artifacts current |
| `npm run db:verify:local` | Existing local `appetee`: counts, relationships, image mappings and recipe readiness passed; no writes |
| `dotnet build Appetee.sln -c Release --no-restore` | Passed, 0 warnings, 0 errors |
| Release tests filtered to `Appetee.Api.Tests.Unit` | 347 passed |
| Release tests filtered to `Appetee.Api.Tests.Diagnostics` | 1 passed in a separate process |
| Release tests filtered to `SchemaAndDatasetIntegrationTests` | 6 passed on MySQL 8.4.9 |
| Existing migration-focused regression tests (filter below) | 24 passed on MySQL 8.4.9 |

The six new integration tests imported all generated SQL into the guarded
disposable `appetee_backend_tests` database. They checked canonical table
presence, counts, relationships, recipe costs and deterministic image names,
partial-schema repair, repeat/data preservation, rejection of incompatible
columns/constraints and missing prerequisites, and an authenticated planner-week
read after repair. Unknown legacy tables in that disposable database are retained;
the baseline owns only its declared tables.

MySQL reported strict mode including `ONLY_FULL_GROUP_BY`,
`STRICT_TRANS_TABLES`, `NO_ZERO_IN_DATE`, `NO_ZERO_DATE`,
`ERROR_FOR_DIVISION_BY_ZERO` and `NO_ENGINE_SUBSTITUTION`.
Its packet limit was 67,108,864 bytes. No application database reset was performed.

Backend test invocation used:

```powershell
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj -c Release --no-build --filter <filter>
```

Database tests required process-scoped `APPETEE_RUN_DB_INTEGRATION_TESTS=true`.
The existing migration-regression filter was:

```text
FullyQualifiedName~DatabaseTests|FullyQualifiedName~F011B01RecipeNutritionTests.Migration_|FullyQualifiedName~F011B02SchemaTests.Upgrade_|FullyQualifiedName~E002CatalogueSecurityTests.AdditiveMigration_|FullyQualifiedName~E001Phase4AccountClosureTests.ForwardMigration_|FullyQualifiedName~AccountClosureSchemaRepairTests
```

These were filtered runs, not the entire backend test suite. Historical
migrations 001-010 and canonical ingredient/recipe JSON/assets were unchanged.
The 2,787 dataset warnings comprise 2,786 possible recipe-similarity warnings
and one main-meal distribution warning; they were not suppressed or mislabeled
as errors. No market-price refresh, recipe deduplication or media approval change
was performed.

Azure repair, production history adoption, deployment, live health checks and
browser/Blob verification are **not performed by this maintenance task**.
