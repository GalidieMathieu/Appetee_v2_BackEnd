# F-011 backend B06 implementation and verification

## Scope and preservation

Backend-only implementation of `Phase_B06_plan_create_and_reads.md` and the
canonical F-011 README. Existing worktree status/diff was inspected before edits.
All prior B01–B05, E-005/E-008, generated SQL, migrations, recipe contracts, and
unrelated changes were preserved. No reset/discard or frontend work was performed.

New authenticated current-user routes:

- `GET /api/nutrition-plans`: coherent bounded summary list and Current context.
- `GET /api/nutrition-plans/{planId:int}`: full stored provenance and profileDiffers;
  missing/foreign IDs are uniformly `404 resource_not_found`.
- `POST /api/nutrition-plans`: strict Easy/Intermediate create, `201` plus canonical
  Location and complete mutation receipt. Requires existing CSRF/Origin transport
  and `X-Meal-Plan-Revision`. No owner/user ID or query parameters are accepted.

Easy calculation/review members are rejected even when explicitly null. All
stored Easy calculation fields remain null; an existing profile is not copied.
Intermediate recomputes with B05, validates the reviewed policy/server UTC year,
stores the complete actual inputs/estimates/targets, and updates the saved profile
only if its four values changed. Policy/year are checked again after acquiring
the user lock to cover a year change while the request waits.

The existing transaction helper owns the complete create: owner lock, revision,
provisional settings, dynamic 50-plan/collation-name checks, optional profile,
plan, Current only when absent, one revision increment, readback, and commit.
Non-Current creation does not invalidate planner contents, even if the profile
changes. No days/meals/items are created. Reads use one REPEATABLE READ snapshot
and a fixed three-result-set query, not per-plan enrichment. Reads never create
settings. Stored targets/age/warnings remain unchanged across UTC-year rollover.

`NutritionPlanSummaryDto` has the canonical 11 fields; `NutritionPlanDto` inherits
them and adds the four detail fields. Current/settings and all three response
envelopes match the canonical contract; JSON revisions are decimal strings.
The existing preview remains unchanged. OpenAPI declares the strict mode union,
review fields, bounds, header requirements, and separate summary/detail schemas.
All new paths are no-store, including unauthorized/security-error responses.
Operation metadata is logged without submitted names, profiles, tokens or bodies.
Unexpected errors at these new boundaries are sanitized even in Development;
unrelated endpoints retain their existing behavior.

## Files changed in this phase

Created:

- `src/Appetee.Application/Requests/MealPlans/NutritionPlanRequests.cs`
- `src/Appetee.Application/Dtos/MealPlans/NutritionPlanDtos.cs`
- `src/Appetee.Application/RowData/MealPlans/NutritionPlanRowData.cs`
- `src/Appetee.Application/Abstractions/MealPlans/INutritionPlanQueries.cs`
- `src/Appetee.Application/Abstractions/MealPlans/INutritionPlanRepository.cs`
- `src/Appetee.Application/Services/MealPlans/INutritionPlanService.cs`
- `src/Appetee.Application/Services/MealPlans/NutritionPlanService.cs`
- `src/Appetee.Application/Services/MealPlans/NutritionPlanMapper.cs`
- `src/Appetee.Infrastructure/MealPlans/NutritionPlanSql.cs`
- `src/Appetee.Infrastructure/MealPlans/NutritionPlanReader.cs`
- `src/Appetee.Infrastructure/MealPlans/NutritionPlanQueries.cs`
- `src/Appetee.Infrastructure/MealPlans/NutritionPlanRepository.cs`
- `src/Appetee.Api/Helpers/NutritionPlanBoundaryAttribute.cs`
- `src/Appetee.Api/Helpers/NutritionPlanSchemaFilter.cs`
- `tests/Appetee.Api.Tests/Integration/F011B06NutritionPlansTests.cs`
- `docs/f011-b06-verification.md`

Modified:

- `src/Appetee.Api/Controllers/NutritionPlansController.cs`
- `src/Appetee.Api/Extensions/ApplicationServiceCollectionExtensions.cs`
- `src/Appetee.Api/Extensions/InfrastructureServiceCollectionExtensions.cs`
- `src/Appetee.Api/Extensions/ApiServiceCollectionExtensions.cs`
- `src/Appetee.Api/Extensions/WebApplicationExtensions.cs`
- `src/Appetee.Application/Models/MealPlans/MealPlanRules.cs`

The phase manifest lists UserSql as an editor. Its existing B04 parameterized
profile read/update statements already satisfy this use case and are reused
on the create transaction; no extra UserSql edit or separate profile commit was
needed. The B06 phase file and its README status/acceptance evidence in the
documentation repository were updated after final verification passed. No other
phase implementation status was changed.

## Database safety and actions

B06 requires no schema change. No migration was added, recreated, overwritten,
or manually applied. `data/generated/sql/01-schema.sql` and existing migration
files remain intact. No production/developer database action or manual destructive
rebuild/reset script was run.

Integration tests explicitly opt in with `APPETEE_RUN_DB_INTEGRATION_TESTS=true`.
The existing fixture enforces loopback MySQL and the fixed database name
`appetee_backend_tests`; it rebuilds only isolated test tables. Synthetic rows,
failure-injection triggers (dropped in finally), EXPLAIN and ANALYZE operations
are confined to that database. Test suites run serially against it.

EXPLAIN validates list/detail/Current/count/name paths using a selective fixture
with 11 owners and 50 plans each, plus a valid Current pointer. Existing indexes
are sufficient; no speculative index was added. The bounded owner list may sort
up to 50 rows for its Current-first expression; this does not require pagination.

## Verification

Commands from the backend root:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
$env:DOTNET_CLI_HOME = 'C:\Users\galid\Documents\Appetee\Backend\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build Appetee.sln
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter FullyQualifiedName~F011B06 --logger 'console;verbosity=normal'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --logger 'console;verbosity=normal'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~F011B06|FullyQualifiedName~Appetee.Api.Tests.Integration.AuthEndpointsTests' --logger 'console;verbosity=normal'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter FullyQualifiedName~F011B06NutritionPlansTests.BoundaryLogging --logger 'console;verbosity=normal'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~F011B06 --logger 'console;verbosity=normal'
```

- Final solution build passed: 0 errors, 6 NU1900 warnings because the NuGet
  vulnerability feed is unreachable. Package vulnerability auditing is unverified.
- Pre-regression named B06 run passed 74/74 real-MySQL integration cases, zero failures
  or skips; reported total time 2.9085 minutes. Runtime .NET 10.0.11 (64-bit).
- Full regression invocation: 684 passed, 1 failed, 0 skipped (685 total;
  reported duration 2.6721 hours). The sole failure was
  `AuthEndpointsTests.Session_ReturnsExpiredOutcome_AtThirtyDayAbsoluteLimit`:
  MySQL command timeout in `ApiTestDatabase.ResetAsync`/fixture initialization,
  before the test body executed. This full-suite invocation is NOT claimed green.
  All other existing tests and all 74 then-existing B06 cases passed.
- After that invocation, the final build passed with 0 errors/6 NU1900 warnings.
  The combined B06/auth rerun passed all 21 auth cases, including the previously
  failed fixture case, and all 74 existing B06 cases. Its sole failure was a new
  logging-test assertion expecting the action filter to log a 415 rejected by
  the earlier Consumes boundary. The test now exercises the feature-owned 400
  instead (and still checks 415 rejection/state safety); focused retry passed 1/1.
  No auth behavior or timeout was changed. Combined run: 95 passed/1 failed,
  reported duration 6.0372 minutes.
- Final all-B06 run after correction passed 75/75, zero failures/skips;
  reported total time 3.2932 minutes. This ran serially on the final successful
  solution build and includes the added response-status/safe-metadata logging case.
  All required B06 build/meaningful tests passed. The earlier full-suite fixture
  timeout and failed logging-test assertion remain disclosed above; no all-green
  full-suite invocation is claimed.

Earlier development runs are not counted as completion: the initial 51-case
slice passed; the expanded run caught development SQL-detail leakage and an
unrepresentative EXPLAIN fixture. Sanitization and the fixture were corrected.
A later helper-reuse build exposed a decimal/int mismatch; the canonical whole
target is now explicitly checked/cast. The focused EXPLAIN test then passed.
An earlier overlapping build temporarily retried a locked assembly copy; the
final build has no MSB3026 warnings. An attempted overlapping test build later
hit CS2012 (locked compiler output); no tests ran in that attempt. After the
successful build finished, the focused rerun uses `--no-build` serially.

## Deployment boundary

No frontend implementation, production deployment or production migration is
claimed. B07 lifecycle mutations and B08+ dated planner endpoints are not part of
this phase. No unresolved product decision was invented.
