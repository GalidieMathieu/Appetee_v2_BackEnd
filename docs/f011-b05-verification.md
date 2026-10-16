# F-011 backend B05 implementation and verification

## Scope and approved decisions

Backend-only versioned Intermediate calculator and calculation preview, following
`Phase_B05_nutrition_calculation.md` and the canonical feature README. The user
explicitly confirmed initial policy version `mp-v1.0.0-2026-10` and midpoint
AwayFromZero for the two-decimal RMR/maintenance response estimates. These
decisions were recorded in the canonical README before implementation.

Existing B01–B04, E-008 tooling, E-005 CSRF, migrations 008/009, generated SQL,
and unrelated dirty/untracked changes were preserved. Current status/diff were
inspected. No reset, discarded changes, overwritten migration, frontend change,
new schema, or later-phase endpoint was introduced.

## Implementation

Authenticated `POST /api/nutrition-plans/calculation-preview` accepts exactly
`{ profile: NutritionProfileRequest, goal, activityLevel }`. It derives session
identity centrally, requires existing E-005 CSRF/allowed origin, accepts no query
parameters or caller identity, and does not require or bind a Meal Plan revision.
Unknown members, client targets/policy/year/age/timestamps, missing/null fields,
numeric codes, numeric strings, nonfinite JSON, and unsupported media types are
rejected. Errors retain existing ProblemDetails with code/traceId and safe
camelCase field paths. Responses, including early 401/CSRF rejection, are no-store.

Application validates complete profile eligibility using the server TimeProvider
UTC year and request-only adult confirmation at age 18. Named domain goal/activity
enums use explicit exact wire-code mappings. The pure calculator implements
Ten Haaf with centimetres converted to metres, approved activity/goal factors,
percentage/absolute caps, energy nearest 25, protein ceiling, fat clamp, residual
carbohydrate, and final positive/fat/energy postconditions using decimal arithmetic.
Unsupported energy is rejected, never clamped. Stable nonblocking warning codes
cover equation population extrapolation and carbohydrate below preference.

Response is exactly `NutritionCalculationDto`: policyVersion, calculationYear,
inputs (profile values plus ageYears/goal/activityLevel), rmrKcal,
maintenanceKcal, targets, warnings. All target fields are known positive whole
units. There are no entity IDs, timestamps, revisions, or adult-confirmation
fields. Full decimal arithmetic determines targets before estimates are rounded.
For the canonical male 80 kg / 180 cm / age 28 / Active example, the exact formula
produces RMR 2005.4844 and maintenance 3509.5977. The earlier README numbers
abbreviated these; its example was corrected without changing coefficients or
targets. Estimates serialize as 2005.48 and 3509.60.

NutritionCalculationService depends only on TimeProvider; pure arithmetic has no
clock, identity, repository, DB, or HTTP dependency. Normal session authentication
still uses the existing session infrastructure; the calculation itself never
reads/writes persistence. API logs operation outcome, elapsed time, zero calculator
row counts, and trace ID without body values, tokens, or user IDs.

## File manifest

Created in the backend:

- `src/Appetee.Application/Models/MealPlans/NutritionPolicy.cs`
- `src/Appetee.Application/Models/MealPlans/NutritionCalculation.cs`
- `src/Appetee.Application/Dtos/MealPlans/NutritionCalculationDto.cs`
- `src/Appetee.Application/Requests/MealPlans/NutritionCalculationRequest.cs`
- `src/Appetee.Application/Services/MealPlans/INutritionCalculationService.cs`
- `src/Appetee.Application/Services/MealPlans/NutritionCalculationService.cs`
- `src/Appetee.Api/Controllers/NutritionPlansController.cs`
- `src/Appetee.Api/Helpers/NutritionCalculationBoundaryAttribute.cs`
- `src/Appetee.Api/Helpers/NutritionCalculationSchemaFilter.cs`
- `tests/Appetee.Api.Tests/Unit/F011B05NutritionCalculationTests.cs`
- `tests/Appetee.Api.Tests/Integration/F011B05PreviewTests.cs`
- `docs/f011-b05-verification.md`

Modified in the backend:

- `src/Appetee.Api/Extensions/ApplicationServiceCollectionExtensions.cs`: scoped service.
- `src/Appetee.Api/Extensions/ApiServiceCollectionExtensions.cs`: bounded OpenAPI filter.
- `src/Appetee.Api/Extensions/WebApplicationExtensions.cs`: no-store for this preview
  path before authorization/CSRF, preserving existing security headers.

Documentation repository: canonical feature README (confirmed decisions and exact
example precision) and B05 phase completion evidence after verification.
NutrientsDto and CalculationInputsDto are shared canonical value types declared
in NutritionCalculationDto.cs, not speculative later-phase endpoints/models.

## Database safety and actions

No new B05 schema/migration or generated SQL changes are required or were made.
Existing `data/generated/sql/01-schema.sql` and migrations remain intact.
Integration tests explicitly opted in with
`APPETEE_RUN_DB_INTEGRATION_TESTS=true`; the fixture enforces loopback MySQL and
fixed `appetee_backend_tests`. Only this isolated fixture rebuilt test tables and
executed synthetic test/migration/constraint work. No manual database reset script
or production/developer database action was run.

## Verification

Commands run from the backend root:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
$env:DOTNET_CLI_HOME = 'C:\Users\galid\Documents\Appetee\Backend\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build Appetee.sln
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter FullyQualifiedName~F011B05
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --logger 'console;verbosity=normal'
```

- Solution build passed: zero errors, six NU1900 warnings because the NuGet
  vulnerability feed was unavailable. Vulnerability auditing remains unverified.
- Final B05 named filter passed 144/144, zero failed/skipped, duration 2m27s
  (83 unit cases and 61 real-MySQL HTTP integration cases).
- Full regression suite passed 611/611, zero failed/skipped. Reported total time:
  15.5903 minutes. This includes existing auth/session, user profile, recipes,
  discovery/favorites, B01–B04, E-005 CSRF, and migration tooling coverage.
- Test adapter reported .NET 10.0.11 (64-bit). No deployment-runtime or production
  database verification is claimed by this backend implementation.

Coverage includes fixed vectors for all 6 goals × 4 activities × both sex
coefficients, golden maintenance/loss targets, 126,720 bounded-grid cases with
postcondition checks, metre conversion, goal caps, energy/macro/estimate rounding
midpoints, early-rounding counterexamples, supported energy endpoints,
feasibility equality/overflow, protein protection, warning thresholds, UTC-year
rollover/adult declaration, cancellation, strict binding/overposting, no revision,
auth/CSRF/no-store/OpenAPI, two-user saved-state isolation, and before/after database
snapshots for successful and rejected previews. The macro-feasibility conflict
is tested directly at the pure target allocator: it is a safeguard and is not
claimed to be reachable through a validated profile in the tested operating grid.

Earlier development runs failed: one expected full-precision estimate incorrectly
used the abbreviated README example; then three HTTP checks exposed missing
no-store headers. These were corrected rather than weakening tests. Those runs
are not counted as successful verification.

## Deployment boundary

Only backend B05 is implemented. Preview computes explicit unsaved inputs without
updating users/settings/plans. Persisting calculations and checking reviewed
policy/year on plan creation/recalculation belong to B06/B07, not this phase.
No frontend implementation or production deployment is claimed.
