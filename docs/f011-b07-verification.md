# F-011 backend B07 implementation and verification

## Scope and preserved work

Backend-only implementation of the canonical F-011 README and
`Phase_B07_plan_lifecycle.md`. Both repositories' status/diff were inspected
before editing. Existing B01–B06, CSRF/security, generated SQL, migration files,
the JSON migrator configuration, and unrelated dirty/untracked work were kept.
No reset/discard, frontend change, or B08+ implementation was performed.

## Endpoints and behavior

- `PUT /api/nutrition-plans/{planId:int}`: required name/count; mode immutable.
  Omit calculation and both review fields for metadata-only. Explicit Intermediate
  recalculation requires all three non-null, uses the existing B05 calculator,
  validates review policy/year before persistence and again after the owner lock,
  and updates the saved profile only when its four values changed. Easy rejects
  calculation/review presence, including explicit null. Metadata-only preserves
  the entire 15-column calculation block and profile. Identical metadata is an
  exact no-op; explicit recalculation advances revision even for matching inputs.
- `POST /api/nutrition-plans/{planId:int}/duplicate`: no body. Generates ` - Copy`,
  then ` - Copy 2` etc. under the lock, truncating/trimming the base within 80
  Unicode scalars and checking real MySQL collation. Enforces the 50-plan cap.
  Copies all source calculation values, including older policy and calculated_at,
  without calculation/profile changes. Fresh ID and UTC creation/update times.
  Never sets Current, including when Current is null. Returns 201 plus Location.
  Candidate selection is bounded and duplicate uses the common 30-second copy
  operation timeout, with 15-second SQL command timeouts.
- `DELETE /api/nutrition-plans/{planId:int}`: no body. Clears a matching Current
  pointer before deleting in the same transaction, with no replacement selection.
  Returns 200, plan:null and deletedPlanId. Repeated/missing/foreign deletion is
  scoped 404, not silent success.
- `PUT /api/meal-plan/current-nutrition-plan`: strict required positive integer
  nutritionPlanId, no null/clear/owner option. Uses stored targets without updating
  plan audit values or recalculating despite profile mismatch. Same Current is a
  no-op, but still requires ownership and a valid current revision.

Every write derives identity from the session, requires CSRF/allowed Origin and
the existing revision header, rejects extra queries, and returns the unchanged
canonical six-field NutritionPlanMutationDto. All affected paths are no-store,
including authentication/security errors and the Current route's trailing-slash
form. Unknown errors are sanitized through the existing feature boundary.
Diagnostics log operation/status/outcome/count/trace metadata, not bodies/names,
measurements or tokens. OpenAPI describes strict update union and Current bounds,
no-body operations, and required security/revision headers.

Infrastructure owns each complete mutation through MealPlanTransaction: one
connection, owner user-row lock, scoped lookup, revision check, dynamic state
checks, mutation, exactly one increment for changes, captured readback, and commit.
No-op readback is captured before rollback of provisional settings. SQL carries
the existing timeout, transaction, cancellation, and bounded deadlock-only retry.
No post-commit unlocked read or independently committed profile update is used.

plannerInvalidated is true only for changed Current selection/deletion, or changed
Current default count/target values. Current name-only and inactive edits/copies
return false. No lifecycle action touches dated days, meals, recipe quantities,
manual details, or their timestamps. Profile mismatch remains visible through
the existing detail profileDiffers contract; no new notice code was invented.

## Files changed by B07

Created:

- `src/Appetee.Api/Controllers/MealPlanSettingsController.cs`
- `tests/Appetee.Api.Tests/Integration/F011B07PlanLifecycleTests.cs`
- `tests/Appetee.Api.Tests/Unit/F011B07PlanRulesTests.cs`
- `docs/f011-b07-verification.md`

Extended existing files:

- `src/Appetee.Api/Controllers/NutritionPlansController.cs`
- `src/Appetee.Api/Helpers/NutritionPlanBoundaryAttribute.cs`
- `src/Appetee.Api/Helpers/NutritionPlanSchemaFilter.cs`
- `src/Appetee.Api/Extensions/WebApplicationExtensions.cs`
- `src/Appetee.Application/Requests/MealPlans/NutritionPlanRequests.cs`
- `src/Appetee.Application/RowData/MealPlans/NutritionPlanRowData.cs`
- `src/Appetee.Application/Models/MealPlans/MealPlanRules.cs`
- `src/Appetee.Application/Services/MealPlans/INutritionPlanService.cs`
- `src/Appetee.Application/Services/MealPlans/NutritionPlanService.cs`
- `src/Appetee.Application/Abstractions/MealPlans/INutritionPlanRepository.cs`
- `src/Appetee.Infrastructure/MealPlans/NutritionPlanRepository.cs`
- `src/Appetee.Infrastructure/MealPlans/NutritionPlanSql.cs`

Existing DI registration is reused; MVC discovers the new controller. Existing
UserSql is reused inside the operation transaction. B06 create/list/detail/preview
DTOs remain unchanged; create and update share the existing reviewed-calculation
validation rather than duplicate arithmetic. The B07 phase and its README
status/acceptance evidence were updated after the final build, named tests and
full regression passed; no unrelated phase status was changed.

## Database actions and safety

B07 adds no schema: no new migration or generated SQL change is needed or made.
No standalone migration CLI invocation, production/developer database action, or
manual destructive rebuild/reset script was run. Existing migrations and canonical
`data/generated/sql/01-schema.sql` were preserved.

Integration runs explicitly set `APPETEE_RUN_DB_INTEGRATION_TESTS=true`.
ApiTestDatabase requires a loopback host and fixes the target to
`appetee_backend_tests`. Only that guarded fixture rebuilds its own test tables.
Synthetic two-user roots, saved days/recipe and manual items, complete stored
calculations, and failure triggers are confined there. Temporary triggers are
dropped in finally. Test suites run serially, not against the local application
database configured for the migrator. The full regression suite also exercises
existing migration SQL/tooling only inside this guarded isolated database.

EXPLAIN checks scoped detail, Current pointer, excluded-self name collision,
owner count, metadata update, delete, clear/set Current paths using the existing
owner/name/primary indexes. An existing second name is used for the collision
lookup because an absent const lookup can be optimized away by MySQL. No
speculative index was added.

## Verification

Commands from the Backend root:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
$env:DOTNET_CLI_HOME = 'C:\Users\galid\Documents\Appetee\Backend\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build Appetee.sln
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~F011B07 --logger 'console;verbosity=normal'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --logger 'console;verbosity=normal'
```

- Solution build passed, zero errors, 8 NU1900 warnings because NuGet vulnerability
  data is unavailable. No new analyzer warning remains; auditing is unverified.
- Focused pure-rule/EXPLAIN check passed 11/11, zero failures/skips.
- Final named B07 passed 82/82, zero failures/skips; 72 real-MySQL integration
  cases and 10 unit cases. Reported time 3.2361 minutes, .NET 10.0.11 (64-bit).
- Full regression passed 779/779, zero failures/skips; reported total time
  24.8887 minutes. Includes B01–B07, migration/configuration tooling, E-005 CSRF,
  auth/session, user profile, discovery/favorites, recipe/admin flows and existing
  account/recovery behavior. No full-suite timeout or retry was needed in this run.

Coverage includes all 15 stored calculation values unchanged on metadata edits,
inactive versus Current invalidation, same-value no-op snapshots, provisional
settings rollback, explicit/repeated recalculation and profile receipts, stale
policy/year plus owner-lock year drift, injected post-profile/plan/revision/delete
failures, exact saved graph preservation, Unicode/collation naming, cap/racing
duplicates, racing Current selections, foreign/missing lookup plus direct composite
FK rejection, stale-tab operations, strict numeric/member/union binding, no-body
actions, auth/revision/CSRF/Origin/query/no-store/OpenAPI contracts, and a throwing
calculator proving metadata/copy/Current do not compute targets.

Development runs are not counted as success: first run 61/62 passed (a fixture's
JSON edit used incorrect inner-property casing); next run 78/80 passed (an absent
EXPLAIN lookup was optimized away, and attribute UTF-8 metadata replaced a lone
surrogate). Fixtures were corrected without weakening the production contract:
web JSON casing, a present lookup, and a runtime-constructed malformed UTF-16
input. The focused corrected checks passed. No product decision was missing.

## Release boundary

Only backend B07 is implemented. No frontend or deployment verification is
claimed. Dated planner endpoints remain B08+; existing saved rows are preserved,
not edited through speculative public endpoints in this phase.
