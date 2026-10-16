# F-011 B10/B11 backend checkpoint — 2026-10-13

Subsequent B12–B15 update (2026-10-15): full backend suite passed 992/992,
zero skipped; the previously noted account-closure/F-011-root cleanup gap is
fixed and verified. See `docs/f011-b12-b15-progress.md`. Earlier evidence below
describes the B10/B11 implementation turn, not the current release-gate status.

Scope: B10 recipe items and B11 manual items only. No frontend application code,
picker navigation state, B12+ endpoint or schema/migration change. Existing dirty
E-002/F-011/CSRF/migrator/data work is preserved.

## Final verified status

Backend B10/B11 implemented and scoped regression verified. Final build passed
with zero warnings/errors (11.39s). Expanded regression passed **348/348**, zero
failed/skipped, **11m37s**, including all **43/43** new B10/B11 cases. TRX counters
were inspected. No test process from this implementation remains running.

The original phase run passed 42/43; its single failure was an overly strict
EXPLAIN assertion on the scalar EXISTS outer row. The assertion was corrected
to check actual table accesses and all phase tests passed in the expanded rerun.
No test is excluded. A fresh full solution suite or browser test is not claimed.

No schema, generated SQL or migration file was changed in this slice. Database
actions were confined to guarded loopback `appetee_backend_tests`: fixture resets,
synthetic users/grants, catalogue nutrient/restriction edits, FK failure checks,
EXPLAIN, controlled insert-failure triggers (removed in finally), a deliberate
manual-integrity fault, and the existing regression migration tests. No developer
or production reset/migration/grant, API launch or Azure action was performed.

API routes implemented: MP06 POST days/{date}/recipe-items; MP07 PUT
recipe-items/{itemId}/quantity; MP08 POST days/{date}/manual-items; MP09 PUT
manual-items/{itemId}; MP10 DELETE items/{itemId}, all below `/api/meal-plan`.
Only add operations return 201 with owning one-date Location; all receipts carry
the documented confirmed context and IDs. Current picker navigation stays browser
only, uses exactly one target, and never materializes state before an add command.

Frontend implementation/Back navigation/browser verification, local/production
rollout, and B12–B15 remain pending/out of scope. The previously documented
account-closure/F-011-root cleanup compatibility gap is not fixed by this slice;
see `e002-b08-b09-verification.md`. It is not a B10/B11 API contract expansion.

Historical checkpoints below are superseded by this final status. Exact build/
test commands and report paths are retained for resumability.

## Current implementation

Created the phase-specified MealItemRequests, IMealItemService/MealItemService,
IMealItemRepository, MealItemRepository and MealItemSql files. Extended pure
MealPlanRules validation, MealPlannerController, scoped DI and OpenAPI filters.
MP06–MP10 use the existing user-lock/revision transaction and captured readback.
Recipe availability uses the shared predicate after a canonical recipe locking
read. Parent creation/manual detail and all ancestor timestamps are atomic.
Exact meal-target union, whole quantities, positive total-entry manual nutrients,
ownership/date/subtype/cap/duplicate validation, no-op updates and generic removal
are implemented. No serving multipliers or snapshots for manual items.

### Files changed by this slice

New production files:

- `src/Appetee.Application/Requests/MealPlans/MealItemRequests.cs`
- `src/Appetee.Application/Services/MealPlans/IMealItemService.cs`
- `src/Appetee.Application/Services/MealPlans/MealItemService.cs`
- `src/Appetee.Application/Abstractions/MealPlans/IMealItemRepository.cs`
- `src/Appetee.Infrastructure/MealPlans/MealItemRepository.cs`
- `src/Appetee.Infrastructure/MealPlans/MealItemSql.cs`

Extended existing work:

- `src/Appetee.Application/Models/MealPlans/MealPlanRules.cs`
- `src/Appetee.Api/Controllers/MealPlannerController.cs`
- `src/Appetee.Api/Extensions/ApplicationServiceCollectionExtensions.cs`
- `src/Appetee.Api/Extensions/InfrastructureServiceCollectionExtensions.cs`
- `src/Appetee.Api/Helpers/MealPlannerSchemaFilter.cs`
- `src/Appetee.Api/Helpers/MealPlannerOperationFilter.cs`

Test and canonical documentation files are listed below and in the B10/B11
phase manifests. Their backend acceptance/status records are aligned with this
verified result; frontend records are not marked implemented.

Picker browser routes are not API routes. Reads/search/preview/cancel have no
materialization command. MP06 materializes only the chosen current default on an
absent day and returns createdMealId/createdItemId plus authoritative context.

## Verification pending

Initial production build passed with zero warnings/errors (21.96s); build with
initial integration coverage passed with zero warnings/errors (24.98s).
Additional boundary/validation tests are saved; final code/test rebuild passed
with zero warnings/errors (16.20s). The named B10/B11 test run is in progress,
with TRX target `.dotnet/test-results/f011-b10-b11.trx`.
Initial run reported an overly strict EXPLAIN assertion for the scalar EXISTS
outer row (no table read). Test now checks index use for actual table-access rows;
initial named-phase run finished 42/43 passed, one assertion failure, zero skips
(2m33s). Rebuild and full named-phase rerun are required. No production defect
was reported by that run; no pass claim is made until the rerun completes.

Corrected build passed: zero warnings/errors, 11.39s. The full B10/B11 named
rerun is now included in an expanded controller/read-model/security/recipe
regression run (not the entire solution test suite), currently in progress:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS='true'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~F011B10|FullyQualifiedName~F011B11|FullyQualifiedName~F011B08|FullyQualifiedName~F011B09|FullyQualifiedName~E002|FullyQualifiedName~E005CsrfTests|FullyQualifiedName~AuthCookieServiceTests|FullyQualifiedName~AuthServiceTests|FullyQualifiedName~F011B01|FullyQualifiedName~RecipeCalculatorTests|FullyQualifiedName~Appetee.Api.Tests.Unit.F008|FullyQualifiedName~Appetee.Api.Tests.Unit.F009|FullyQualifiedName~Appetee.Api.Tests.Unit.F010|FullyQualifiedName~AdminEndpointsTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=f011-b10-b11-regression.trx' --results-directory .dotnet/test-results
```
Do not mark phases complete until meaningful named tests and regression pass.

Test files: Integration/F011B10RecipeItemTests.cs,
Integration/F011B11ManualItemTests.cs, Unit/F011B11ManualItemRulesTests.cs and
Infrastructure/MealItemTestBase.cs. One older B09 OpenAPI assertion is updated
to recognize the now-implemented B10 recipe-item route.

Only isolated guarded loopback `appetee_backend_tests` with explicit
APPETEE_RUN_DB_INTEGRATION_TESTS=true may be reset/seeded. No local/production
migration, reset, grant, or backend service launch is authorized by this slice.

Resume commands from Backend:

```powershell
dotnet build Appetee.sln --no-restore --verbosity minimal
$env:APPETEE_RUN_DB_INTEGRATION_TESTS='true'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~F011B10|FullyQualifiedName~F011B11' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=f011-b10-b11.trx' --results-directory .dotnet/test-results
```
