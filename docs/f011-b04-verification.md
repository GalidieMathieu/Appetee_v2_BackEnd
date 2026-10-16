# F-011 backend B04 implementation and verification

## Scope and preservation

Backend-only B04, following the canonical feature README and
`Phase_B04_nutrition_profile.md`. Existing B01 recipe projections/fat persistence,
B02 schema and migrations 008/009, B03 write protocol, E-008 tooling, E-005 CSRF,
and unrelated working-tree changes were preserved. Git status and current diff
were inspected before editing. No migration was overwritten, recreated, or
renumbered. No frontend or later-phase endpoints were implemented.

## Endpoint implementation

- Authenticated `GET /api/users/me/nutrition-profile` returns exactly
  `{ revision: string, profile: complete-value | null }`. The value contains
  `calculationSex`, `birthYear`, `heightCm`, and `weightKg`. A single parameterized
  owner-scoped join reads the profile and effective revision in a coherent
  REPEATABLE READ transaction. An absent profile returns 200/null without writes.
- Authenticated `PUT /api/users/me/nutrition-profile` requires a complete strict
  JSON body, existing E-005 CSRF transport, and `X-Meal-Plan-Revision`. It rejects
  unknown/missing/null fields, numeric strings, numeric sex codes, query
  parameters, and caller user/owner IDs. Sex is exactly male/female, height
  120–230 cm, weight 35–250 kg, and measurements have at most two decimal places.
  Server UTC year determines age 18–65; age 18 requires request-only
  `adultConfirmed: true`.
- PUT reuses `MealPlanTransaction`: lock the current user, compare revision,
  update only four nutrition columns, increment once for a change, capture
  readback before commit. Identical values are a no-op but still require the
  current revision; provisional settings are rolled back. The existing
  MySQL CURRENT_TIMESTAMP strategy owns `users.updated_at`.
- Existing username/image responses, legacy route containment, and saved plan
  inputs/targets/calculation timestamps remain unchanged. No profile save
  recalculates a plan. DTO revision writes as a decimal string; numeric request
  inputs remain strict. OpenAPI mirrors the separate contract.

## B04 file manifest

Created:

- `src/Appetee.Application/Dtos/MealPlans/NutritionProfileDto.cs`
- `src/Appetee.Application/Requests/MealPlans/NutritionProfileRequest.cs`
- `src/Appetee.Application/RowData/MealPlans/NutritionProfileRowData.cs`
- `src/Appetee.Api/Helpers/NutritionProfileBoundaryAttribute.cs`
- `src/Appetee.Api/Helpers/NutritionProfileSchemaFilter.cs`
- `tests/Appetee.Api.Tests/Integration/F011B04ProfileTests.cs`
- `docs/f011-b04-verification.md`

Modified:

- `src/Appetee.Api/Controllers/UsersController.cs`
- `src/Appetee.Api/Extensions/ApiServiceCollectionExtensions.cs`
- `src/Appetee.Application/Services/Users/IUserService.cs`
- `src/Appetee.Application/Services/Users/UserService.cs`
- `src/Appetee.Application/Abstractions/Users/IUserQueries.cs`
- `src/Appetee.Application/Abstractions/Users/IUserRepository.cs`
- `src/Appetee.Application/Models/MealPlans/MealPlanRules.cs`
- `src/Appetee.Infrastructure/Users/UserQueries.cs`
- `src/Appetee.Infrastructure/Users/UserRepository.cs`
- `src/Appetee.Infrastructure/Users/UserSql.cs`
- `tests/Appetee.Api.Tests/Unit/UserServiceTests.cs`

Completion status and acceptance evidence were also recorded in the documentation
repository's `docs/features/F-011-meal-plan/backend/Phase_B04_nutrition_profile.md`.

The two API helpers limit binding/error and schema alignment to this contract,
without refactoring unrelated E-010 behavior. Existing service registrations
and TimeProvider are reused. UserRepository is internal to consume the existing
internal transaction helper; its application interface remains the boundary.

## Database safety and actions

Integration tests explicitly opted in with
`APPETEE_RUN_DB_INTEGRATION_TESTS=true`. The fixture enforces loopback MySQL and
the fixed `appetee_backend_tests` database. Only this isolated fixture rebuilt
test tables and executed synthetic data/migration/constraint tests. No manual
database rebuild/reset script, production/developer database migration, schema
regeneration, or new B04 migration was run. B04 uses existing B02 columns, so
`data/generated/sql/01-schema.sql` and migrations need no B04 changes.

## Verification

Commands run from the backend root:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
$env:DOTNET_CLI_HOME = 'C:\Users\galid\Documents\Appetee\Backend\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build Appetee.sln
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter FullyQualifiedName~F011B04
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore
```

- Solution build passed: zero errors, six NU1900 warnings because the NuGet
  vulnerability feed was unreachable. Package vulnerability auditing could not
  complete; this is not a clean security-audit result.
- Final named B04 run passed 50/50, zero failed/skipped, duration 2m16s.
- Full regression suite passed 467/467, zero failed/skipped, duration 15m24s.
  This includes existing auth, username/image, recipes/discovery/favorites,
  B01/B02/B03, migration tooling, and E-005 CSRF coverage.
- Earlier named runs failed during development. Record property validation
  annotations caused MVC record-binding errors; removing inappropriate
  DataAnnotations while retaining strict JSON-required metadata corrected them.
  Test response stream reads, string-revision deserialization, and empty-header
  transport expectations were corrected. Those failed runs are not counted as
  successful verification.

Coverage includes absent read/no writes, complete boundary saves/readback,
invalid bodies and unchanged database snapshots, stale revisions/no-op settings,
UTC-year/adult declaration, two-user scoping, anonymous access, CSRF rejection,
legacy shape/404 containment, saved-plan preservation, media/body boundaries,
OpenAPI, and EXPLAIN primary-key access. The controlled meal/profile race runs a
real meal write through the shared transaction helper while the HTTP profile
write waits on the same owner lock; the stale profile request returns 409 and
writes nothing. It does not introduce a speculative later-phase meal endpoint.

## Deployment boundary

Only backend B04 is implemented. The paired frontend must consume the separate
nutrition-profile API and use the existing CSRF/revision transport. No client
implementation or production deployment is claimed here.
