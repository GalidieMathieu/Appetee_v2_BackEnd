# F-011 B12–B15 backend checkpoint — 2026-10-15

Scope: backend dense ordering, exact day/week copy and release verification.
Preserve all pre-existing dirty E-002/E-005/E-008/F-011 work and migrations.
No frontend code or new schema is required. Do not run developer/production reset,
blind migration replay, permission grants or deployment actions.

## Final verified handoff — 2026-10-15

- B12 dense ordering, B13 exact Copy Day and B14 atomic Copy Week are implemented.
  B15 automated backend release verification is implemented and passed; external
  deployed release gates below are still open. Do not label the deployed or
  cross-stack release complete.
- Final `dotnet build Appetee.sln --no-restore --verbosity minimal`: **0 warnings,
  0 errors**, **12.23s**. No application/test source changes after this build.
- Required full backend suite: **992/992 passed**, **0 failed, 0 skipped**, exit 0,
  **41.1526 minutes** (41m09s). Includes **49/49 B12–B15 tests**. TRX counters
  inspected: no errors/timeouts/aborts/inconclusive/not-executed cases.
- Artifact: `.dotnet/test-results/f011-backend-release-full.trx`. Earlier scoped
  runs also passed (29/29, 17/17, final-contract 4/4); no test excluded. Node
  tooling tests passed **48/48**, 0 skipped; canonical/generated schema aligned.
- Final full-run maximum mixed-week measurement: 7 days / 84 meals / 1680 items;
  one range command / four result sets, **41ms**, **425658 bytes**; copy **18 ordinary
  / 36 maximum commands**, **128-row batches**, **2611 reported inserted rows**,
  **301ms transaction / 333ms TestServer receipt**. Local loopback MySQL only;
  not a production latency/SLO/network claim. MySQL 8.0.35, packet 67108864,
  strict SQL mode/InnoDB/checks and indexed access verified as recorded below.
- The prior F-011-root/account-closure gap is fixed and verified, including owner
  isolation, outbox/root rollback and existing E-001/E-002 retention regressions.
- Database actions: only opted-in guarded `appetee_backend_tests` fixture resets,
  synthetic data/permission changes, existing immutable-migration upgrade/repair
  tests, controlled faults/cancellation, EXPLAIN, and connection-local gapped
  auto-increment proof. Triggers removed in finally. No developer/production
  migration, reset, real account grant, backend listener launch or Azure action.
- No new/modified migration or schema/generated SQL in this slice. Existing dirty
  work was preserved. All verification processes for this implementation finished.

The checkpoints below are historical; this final handoff supersedes their
running/pending-suite wording. Exact executed commands and changed-file manifest
are retained below. The deployment checklist remains outstanding, not superseded.

Current state: specifications and master architecture/API read; B12 request,
validation, service/repository methods implemented. Controller/OpenAPI and copy
slice/tests still in progress. No completion claim. B15 must run the full guarded
suite and record real MySQL/version/SQL-mode/packet/query-count/elapsed evidence.
Deployment identity separation, deployed migration history/recovery and browser
verification require external evidence and cannot be inferred from test results.

Only guarded loopback `appetee_backend_tests` with explicit
`APPETEE_RUN_DB_INTEGRATION_TESTS=true` may be reset by the test fixture.

## Checkpoint 2

B12–B14 production slices, DI and OpenAPI are implemented. Copy locks recipe
references in ascending ID order, captures complete sources before deleting,
batches inserts with a packet-aware maximum of 128 rows, and maps generated IDs
by owner/date/parent/order, not contiguous auto-increments. No-op/rollback keeps
first-created settings transient. Existing transaction logs now include bounded
SQL command counts and Execute-reported row counts without payloads.

B15 includes the minimal master-specified account-closure integration fix:
under the existing user lock/transaction remove settings, days/cascades, plans,
then user. Existing E-001 outbox and E-002 audit retention are unchanged. Prior
handoff's known F-011-root closure gap is being addressed, not silently ignored.

Initial production build passed with zero warnings/errors (19.45s). Initial
B12/B13/B14 integration suites are saved and running; new B15 security/performance
and cancellation/ambiguous-response tests are still being completed. Do not mark
any phase complete until meaningful tests pass. Initial report target:
`.dotnet/test-results/f011-b12-b14-initial.trx`.

## Checkpoint 3 — release tests saved

Initial B12/B13/B14 integration run: 29/29 passed, zero skipped, 1.5799min.
Initial B15 run: 17/17 passed, zero skipped, 1.0225min. It verifies every feature
route anonymously and all unsafe routes with missing/invalid CSRF, mid-copy SQL
cancellation rollback, a lost response after successful commit (GET reconciliation
and stale original intent rejection), strict boundaries, account closure and
rollback, OpenAPI and indexed maximum mixed-week copy.

Measured isolated environment: MySQL 8.0.35, InnoDB; SQL mode
ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,
ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION; max_allowed_packet=67108864.
.NET SDK 10.0.400, test runtime 10.0.11; Dapper 2.1.66/MySqlConnector 2.5.0.
Range: 1 command / 4 sets, 7 days / 84 meals / 1680 mixed items, 32ms,
425658 serialized bytes. Ordinary copy=18 commands; max mixed copy=36 commands,
128-row batches, 2611 Execute-reported inserted rows, 334ms transaction / 361ms
receipt. These are local observations, not a production latency SLO. Transaction
command metrics exclude begin/commit and cookie-authentication checks. Row counts
include Execute-reported effects only, not legacy scalar-returning inserts or
InnoDB cascade counts. Indexed access: owner/date uq_meal_plan_days_user_date,
parent/order idx_meal_plan_day_meals_order, item parent lookup
uq_meal_plan_meal_items_recipe, recipe PRIMARY.

Subsequent additions: pure copy-builder tests, gapped auto-increment integration
proof, and endpoint-specific OpenAPI 12-meal bound. Final-contract check is running
before the required full suite. Latest build: zero warnings/errors, 12.23s.
Canonical `scriptDatabase.sql` and generated `data/generated/sql/01-schema.sql`
were compared exactly after normalizing line endings: aligned. No migration or
generated SQL edits. Dataset tooling `npm test` is running; no reset/generation,
Azure deployment or developer/production database operation is performed.

## Checkpoint 4 — full suite running

Final contract/ID check passed 4/4 (14.1014s); dataset tooling tests passed 48/48
(11.8218s), zero failures/skips. No additional source changes after this build.
The required full backend suite is now running; do not claim B15 complete until
it passes. Exact commands from Backend:

```powershell
dotnet build Appetee.sln --no-restore --verbosity minimal
$env:APPETEE_RUN_DB_INTEGRATION_TESTS='true'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --logger 'console;verbosity=normal' --logger 'trx;LogFileName=f011-backend-release-full.trx' --results-directory .dotnet/test-results
```

Other commands/results:

```powershell
# Initial phase tests: 29/29; 1.5799min
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~F011B12|FullyQualifiedName~F011B13|FullyQualifiedName~F011B14' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=f011-b12-b14-initial.trx' --results-directory .dotnet/test-results
# Initial B15: 17/17; 1.0225min
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~F011B15' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=f011-b15-initial.trx' --results-directory .dotnet/test-results
# Final builder/ID/OpenAPI checks: 4/4; 14.1014s
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~F011B13CopyBuilderTests|FullyQualifiedName~GappedAutoIncrementIds|FullyQualifiedName~OpenApi_AllFour' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=f011-final-contract-check.trx' --results-directory .dotnet/test-results
# From data/tools, tests only: 48/48
npm test
```

If interrupted, inspect the saved full-suite TRX and any still-running testhost
before rerunning; never run concurrent database-resetting suites on the same
isolated database. Deployed migration history, identity separation/permissions,
backup/restore readiness and browser/client release evidence remain external
rollout gates even if the local suite passes. No production readiness claim.

## Changed-file manifest for this slice

New production files:

- `src/Appetee.Application/Requests/MealPlans/ReorderRequest.cs`
- `src/Appetee.Application/Requests/MealPlans/CopyRequests.cs`
- `src/Appetee.Application/Models/MealPlans/PlannerCopyBuilder.cs`
- `src/Appetee.Application/Services/MealPlans/IPlannerCopyService.cs`
- `src/Appetee.Application/Services/MealPlans/PlannerCopyService.cs`
- `src/Appetee.Application/Abstractions/MealPlans/IPlannerCopyRepository.cs`
- `src/Appetee.Infrastructure/MealPlans/PlannerCopyRepository.cs`
- `src/Appetee.Infrastructure/MealPlans/PlannerCopySql.cs`

Extended existing production work:

- `Services/MealPlans/IMealService.cs`, `MealService.cs`, `IMealItemService.cs`,
  `MealItemService.cs` in Application; matching `IMealRepository.cs` and
  `IMealItemRepository.cs` in Application/Abstractions/MealPlans.
- `Models/MealPlans/MealPlanRules.cs` in Application.
- Infrastructure/MealPlans `MealRepository.cs`, `MealItemRepository.cs`,
  `MealSql.cs` (compaction helper comment), `MealPlanTransaction.cs` (safe metrics).
- Api `Controllers/MealPlannerController.cs`, DI extension files and
  `Helpers/MealPlannerSchemaFilter.cs` / `MealPlannerOperationFilter.cs`.
- Infrastructure/Users `AccountClosureRepository.cs` / `AccountClosureSql.cs`
  for the master-defined restrictive-FK root deletion order.

New tests/support:

- Integration `F011B12OrderingTests.cs`, `F011B13CopyDayTests.cs`,
  `F011B14CopyWeekTests.cs`, `F011B15SecurityAndConcurrencyTests.cs`,
  `F011B15PerformanceTests.cs`.
- Unit `F011B13CopyBuilderTests.cs`.
- Infrastructure `PlannerCopyTestBase.cs` and `MealPlanTestDiagnostics.cs`.
- Extended test factory, guarded environment query and fixture resets for test-only
  metrics/reply-fault support. Updated older B09/B11 OpenAPI route-inventory
  assertions to recognize the now-implemented Copy Week endpoint.

Canonical master and backend B12–B15 phase docs record actual verification status;
all frontend files, existing migration files, dataset records/generator and schema
remain unchanged by this slice. Prior dirty schema/generated/migration work is
preserved, not reset or recreated.

## Deployment gates — not executed against a developer/remote target

1. Identify the intended target and deployed build/commit. Have the trusted
   operator verify MySQL >=8.0.16, strict SQL mode, InnoDB/CHECK/FK enforcement,
   packet size and actual schema before any migration. Read-only preflight SQL:

   ```sql
   SELECT DATABASE(),VERSION(),@@sql_mode,@@max_allowed_packet;
   SELECT migration_id,checksum_sha256,execution_kind,applied_at_utc
   FROM appetee_schema_migrations ORDER BY sequence_number;
   SHOW GRANTS FOR CURRENT_USER();
   ```

   If the history table is absent, stop and use the audited E-008 adoption process;
   do not treat it as an empty new database or replay historical migrations.
   Preserve immutable migrations 001–010. B12–B15 need no additional migration.
2. Verify a recoverable backup/PITR point and fix-forward procedure. Run any
   approved pending 008/009/010 through the existing migrator with its private
   `appsettings.migrations.json`, not reset/bootstrap scripts. See
   `docs/database-migrations.md`. No credentials belong in these reports.
3. Demonstrate distinct least-privileged runtime/migration identities and their
   real grants. Runtime must not have schema DDL/GRANT authority. The isolated
   fixture uses DDL-capable credentials by explicit opt-in; that is NOT proof of
   deployed identity separation. Trusted E-002 operator assignment/revocation and
   audit are separate from ordinary planner access; grant neither permission
   automatically and never expose a self-service assignment endpoint.
4. With explicit target approval, use synthetic accounts for authenticated and
   credentialed/CSRF-protected read/write/copy/reorder smoke checks, no-store,
   exact JSON/error/OpenAPI alignment, ownership, stale 409 and missing/invalid
   CSRF denial. Do not use real personal profile/manual food records for tests.
5. Deploy/verify the paired frontend and browser acceptance separately. Record
   environment/commit/time/results and monitor safe operation/status/latency/
   conflict metrics. Preserve saved data on rollback; do not drop planner tables.

Local automated verification can pass while these operational gates are still
open. The full cross-stack release must not be marked complete from local docs
or test results alone.
