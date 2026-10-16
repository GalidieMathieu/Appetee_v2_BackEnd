# E-002 and F-011 B08/B09 implementation checkpoint

Updated: 2026-10-12. Backend only. This is a resume checkpoint, not completion evidence.

## Final handoff status

Subsequent approved E-002 session-capability extension (2026-10-12) is tracked in
`docs/e002-session-permissions-verification.md`. Its checkpoint/results supersede
earlier session-contract assumptions; F-011 scope is unchanged.

- Authorized E-002 backend and F-011 B08/B09 are implemented and scoped tests
  verified. B10-B15 and frontend work are NOT implemented.
- Final build: zero errors/warnings, 16.63s.
- Combined scoped suite: 192/192 passed, zero failed/skipped, 9m04s.
- Initial full suite: 883/885 passed, two failures (isolated reset timeout and
  old upgrade-fixture mismatch). The fixture was corrected without changing
  migration files/runtime; affected rerun passed 9/9, zero failed/skipped, 26s.
- No fresh clean full-suite run after that fixture correction is claimed. All
  reports are retained in .dotnet/test-results. No test process remains running
  from this implementation turn.
- Complete implementation/commands/evidence/limitations are in
  docs/e002-b08-b09-verification.md; operator instructions in
  docs/e002-catalogue-operations.md.
- No developer/production migration or grants were executed. Migration 010 and
  audited existing-history adoption are deployment tasks. Account closure with
  F-011 roots has the separate pre-existing compatibility gap documented below.

Historical progress entries follow; do not interpret old pending-run lines as
current state. Resume from this final handoff and the newest user instruction.

## Latest user authorization

Latest user instruction narrows scope to E-002 and F-011 B08/B09 ONLY.
B10-B15 and all frontend work are out of scope. Preserve existing work and
save progress for an interrupted session. Do not invent missing specification
contracts; ask the user before choosing them. No frontend work is authorized.

## Initial inspection (historical; superseded by implementation slices below)

- Inspected backend/docs git status and relevant current diffs. Both worktrees
  contain substantial earlier modified/untracked work. Do not reset, overwrite,
  delete, recreate migrations, or commit unrelated changes.
- F-011 B07 is already implemented and verified. B08-B15 are not implemented.
- Read E-002 README and backend.md. E-002 is not implemented: AdminController
  uses plain Authorize; its Admin role restriction is commented out.
- AuthCookieService issues ID/name/original-issued-time claims, without durable
  permissions or a security-version claim. Cookie validation checks absolute
  lifetime and account existence, not permission revocation or disabled state.
- No approved catalogue optimistic-concurrency request/response contract was
  found. E-002 explicitly leaves direct versus role-based permission storage
  and revalidation frequency to be defined.
- Planner DTOs require warnings arrays, but F-011 does not define warning codes
  or their aggregation rules for unavailable recipes and unknown fat.
- No production code, tests, schema, migrations, or database data changed in
  this continuation. Only this checkpoint was added. No build/tests run here.

## Approved decisions and contract update

User approved the permission/precondition/warning decisions on 2026-10-12.
Canonical E-002 backend.md and README, and F-011 README warning rules were
updated before coding. Implementation is in progress, not verified complete.

### Saved E-002 implementation slice

- Added migration 010 (20261012_010_e002_catalogue_permissions.sql); preserved
  001-009. Updated scriptDatabase.sql and regenerated canonical 01-schema.sql
  through the dataset generator. No migrations run on local appetee.
- Added Application catalogue contracts/actor context, Infrastructure live
  permission queries and transaction-side authorization/version/audit helper.
- Added named policies, cookie security-version/active-account validation,
  admin read/write enforcement, admin ETag envelopes and If-Match parsing.
- Added trusted migrator permission command with atomic change/audit/revocation.
  No automatic or real-account grants were executed.
- Added OpenAPI catalogue operation filter and isolated E002 security tests;
  adapted existing authoring/CSRF/fat tests for explicit test provisioning.
- First build passed, 0 errors and one duplicate-using warning (subsequently
  removed). Final build/test validation remains required.
- Dataset tooling: npm test passed 48/48; npm run validate passed 121 ingredients
  /1699 recipes, 0 errors, 2787 existing warnings; npm run build passed.
- First targeted run: 108 passed, 2 failed (legacy CSRF fixtures lacked catalogue
  permissions; rejected with 403 before CSRF). Those synthetic fixtures were
  explicitly provisioned; production authorization was not weakened.
- Latest targeted command (backend root):
  `dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~E002CatalogueSecurityTests|FullyQualifiedName~AdminEndpointsTests|FullyQualifiedName~AuthCookieServiceTests|FullyQualifiedName~F011B01RecipeNutritionTests|FullyQualifiedName~E005CsrfTests' --logger 'console;verbosity=minimal'`
  with `APPETEE_RUN_DB_INTEGRATION_TESTS=true`: 114/114 passed, zero failed/skipped,
  4m57s. Includes new forged/legacy/stale-cookie and migration cases. This is not
  full regression or cross-stack E-002 completion.

### Saved B08 implementation slice

- Added normalized DTO union, raw RowData, exact defaults, per-nutrient arithmetic
  and read-model builder; unavailable items remain visible and warn correctly.
- Added four-result-set SQL/reader/query with read-only REPEATABLE READ snapshot;
  shared compatibility predicate, no per-item preview/detail calls.
- Added GET /api/meal-plan/days, strict range/query validation, DI and no-store.
- Added F011B08 unit and isolated integration tests. Targeted test build/run is
  starting; no success claim until results are captured.
- Interrupted run's final output was unavailable on resume, so B08 was rerun:
  `dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~F011B08' --logger 'console;verbosity=minimal'`
  with explicit DB opt-in: 34/34 passed, zero failed/skipped, 56s. The MySQL
  UInt16/int sort-order mapping fix is present. Further coverage/final build
  remain to be verified before final phase status changes.

### Saved B09 implementation slice

- Added strict CreateMealRequest/RenameMealRequest, IMealService/MealService,
  IMealRepository/MealRepository and MealSql. Reused existing transaction and
  four-result-set in-transaction planner reader.
- POST days/{date}/meals creates only one selected/explicit meal, validates
  Current virtual slot under lock, enforces cap 12 and appends dense order.
- PATCH meals/{id:int} trims/NFC-validates names; same value is a no-op.
- DELETE meals/{id:int} scopes ownership, cascades children, compacts surviving
  order in one parameterized batch, retains the saved day including final empty.
- All responses return captured normalized day/context/revision. Created meal
  returns 201 and one-date Location; no new schema/migration for B08/B09.
- Added F011B09 isolated tests; targeted build/run is pending actual result.
- No B10-B15 endpoints, services or repositories were added.
- Added projection validation before the mutation transaction commits. Corrupt
  manual readback now rolls back rename/revision/timestamps instead of failing
  only during post-commit DTO mapping.
- Added scoped OpenAPI schema/operation filters; independent admin read/write
  policies, catalogue If-Match and planner revision remain separate contracts.
- B08 additional coverage proves one application command for both empty and
  maximum 7x12x20-item ranges, coherent snapshots across concurrent changes,
  corruption rejection and Intermediate targets on past/future dates.
- B09 coverage includes ownership, 12-meal cap, concurrent first writes,
  strict numeric/unknown fields, CSRF, header/media errors, cascades, compaction,
  saved-empty reload and rollback of corrupt readback.
- Development test results: first B09 run 21 passed/4 assertion failures
  (nested collection reference comparison and settings-count expectations),
  corrected. Expanded B08/B09 run 70 passed/1 failed: duplicate normalized
  model-state keys made empty JSON return 500. Fixed by grouping error paths.
  Latest expanded scoped run is pending completion; saved TRX path is
  .dotnet/test-results/b08-b09-targeted.trx.
- The expanded run finished: 75/75 passed, zero failed/skipped, 2m27s. Subsequent
  final changes add culture-invariant keys/Location, admin precondition checking
  before body binding, and admin no-store.
- `dotnet build Appetee.sln --no-restore --verbosity minimal` passed after those
  changes: zero errors/warnings, 23.36s. Combined E002/B08/B09/legacy admin/auth/
  CSRF/fat tests are now running against the same built binaries. TRX output:
  .dotnet/test-results/e002-b08-b09-targeted.trx. Capture results before marking
  scoped verification complete.
- Final combined targeted run passed: 192/192, zero failed/skipped, 9m04s.
  Report: .dotnet/test-results/e002-b08-b09-targeted.trx. This includes final
  precondition/culture/no-store changes and tests.
- Full regression is starting with unchanged built binaries:
  `dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --logger 'console;verbosity=normal' --logger 'trx;LogFileName=e002-b08-b09-regression.trx' --results-directory .dotnet/test-results`
  and explicit integration opt-in. Report is persisted under .dotnet/test-results
  when the run finishes. Do not call it passed until the result is captured.
- Current unified-exec session for that run: 25880. If it is unavailable after
  interruption, inspect the saved TRX result instead of assuming success; do
  not start another DB suite while the old test process is still running.
- Full run observed a failure before the test body:
  E002CatalogueSecurityTests.Reads_Anonymous401_Ordinary403, ingredient route;
  MySQL command timeout in ApiTestDatabase.ResetAsync while executing the
  guarded isolated bootstrap script (configured setup timeout 120s). Do not
  claim full regression green. Let the run finish; inspect TRX and rerun failing
  cases separately. Do not weaken safety guards or feature command timeouts.
- Full run also found a legacy upgrade fixture mismatch:
  F011B02SchemaTests.Upgrade_PreservesExistingRowsAndMigrationHistoryHandlesApplyAgain
  baselined through 008 but left E-002/010 columns/tables from current bootstrap,
  so 010 correctly rejected a duplicate constraint. Updated only that synthetic
  fixture to remove the later 010 schema first; added assertions that 010 applies
  once and grants nobody. Migration files and runtime behavior are unchanged.
  This test-only change needs a build/rerun after the current full run completes.
- Full run finished: 885 total, 883 passed, 2 failed, zero skipped; 39.8332
  minutes. Failures are the reset-timeout case and the legacy fixture mismatch
  above. Original TRX retained; full suite is NOT called green. Final rebuild
  and affected-test reruns are next; no runtime or migration changes required.

### Known deployment and out-of-scope limitations

- Migration 010 has NOT run on local appetee or production. Deploy it before
  restarting the updated API; authentication now reads its security columns.
  Existing local history was empty at the earlier read-only inspection and
  requires audited adoption, not blind migration replay.
- No real-account grants were run. Existing/ordinary users remain unprivileged;
  trusted operators grant read/write separately, with auditable identity.
- Old cookies without the security-version claim require sign-in after deploy.
- E-002 frontend work is not implemented. No new current-session capability
  response fields or permission-management HTTP API were invented.
- Static inspection found pre-existing account closure deletes users directly
  without deleting the F-011 settings/day/plan roots first; those existing FKs
  restrict deletion when planner data exists. AccountClosureSql/Repository were
  not changed in this narrowed scope. Treat that separate compatibility issue
  as a deployment/release limitation, not an untested successful cleanup.
- The unrelated removal of an unused ImageSharp package reference in the
  Infrastructure project was already present on resume and was preserved.

1. Direct durable account permissions Catalog.ReadAdmin and Catalog.Write,
   with named backend policies, no automatic grants, and a documented trusted
   assignment/revocation procedure. Check current permission/active-account
   state on every admin request and recheck authorization inside write
   transactions. Use account security-version state to reject stale tickets.
   Add catalogue version metadata and require If-Match for existing catalogue
   updates, documenting the wire/error contract before implementing it.
   Record actor/operation/target/version/time/correlation metadata without
   bodies or health/nutrition payloads.
2. Planner warning codes recipe_fat_unknown and recipe_unavailable, propagated
   as deduplicated warnings from recipe items to meals and days.

Approval details: admin reads require Catalog.ReadAdmin; mutations require
Catalog.Write, separately granted. No automatic/self-service grants. Resource
read ETag and update If-Match; missing 428, stale 412 with atomic no-change.
Catalogue versions remain separate from Meal Plan revision. Permission changes
must record actor/time. Unavailable items receive recipe_unavailable alone;
unknown-fat available recipes receive recipe_fat_unknown; meal/day warnings
deduplicated. Follow the canonical backend document for exact persistence/wire
details defined before implementation.

## Remaining authorized implementation sequence

1. Define the approved E-002 backend contracts, implement policy persistence,
   enforcement, audit/concurrency and guarded real-MySQL tests. No frontend.
2. B08 normalized range reader/builder and nutrition/default rules.
3. B09 sparse meal create/materialize/rename/delete with caps, compaction,
   parent retention, user-scoped transactions and confirmed readback.
4. Verify scoped security/concurrency/fault/OpenAPI coverage, build and relevant
   regression tests. Update real evidence. Do not implement B10-B15.

Update this checkpoint after each meaningful implementation/test result.
Do not mark any phase verified unless its meaningful tests and build pass.

## Safety and repository references

- Backend root: C:/Users/galid/Documents/Appetee/Backend.
- Docs root: C:/Users/galid/Documents/Appetee/appetee-docs.
- Canonical E-002 spec:
  docs/engineering/initiatives/E-002-administrative-permissions/{README,backend}.md
  in the docs repository.
- Canonical F-011 spec: docs/features/F-011-meal-plan/README.md and backend
  phase files in the docs repository.
- Before data/tooling changes, read Backend/data/AGENTS.md completely and its
  required references. Canonical production schema is
  data/generated/sql/01-schema.sql; align generation via the data instructions,
  rather than manually diverging generated SQL.
- Preserve migrations 001-009 and checksums. Allocate any new E-002 migration
  against the actual migration inventory; never rewrite prior migrations.
- Existing migrator uses private appsettings.migrations.json. Never print
  credentials or dump private settings. Do not auto-apply migrations or grant
  privileges to developer/production accounts.
- Database tests require APPETEE_RUN_DB_INTEGRATION_TESTS=true and the existing
  guard: loopback host, fixed appetee_backend_tests database. Do not run a
  destructive rebuild against local appetee or any other configured database.
- Tests/builds should run sequentially to avoid assembly locks and DB-suite
  interference. Previous runs found ImageSharp NU1902/NU1903 advisories; do not
  label the vulnerability audit clean or silently expand into package upgrades.
- Earlier missing account_closure_cleanup was repaired locally using the
  narrow repair-account-closure command. Local migration history remains empty
  and needs an audited adoption process, not blind migration replay.
- Prior empty chicken discovery was diagnosed read-only: 142 matching recipes,
  account 1 diet intersection excludes all, account 3 excludes chicken, account
  2 has 117 compatible chicken recipes. No recipe/preference change was made.

## Resume instruction

Read this checkpoint, the user's decision, applicable instructions and current
git status/diff before editing. Continue from actual saved source, not from an
assumption that planned work or tests have already been completed.
