# E-002 / F-011 B08 and B09 backend verification

Date: 2026-10-12. Backend-only E-002/B08/B09 implementation and scoped verification
complete. Initial full regression had two failures; affected-test rerun passed.
A fresh entirely green full-suite run after the fixture correction is not claimed.

## Scope and preservation

Latest user scope is E-002, B08 and B09 only. B10-B15 and frontend work are not
implemented. Existing B01-B07, security/migrator work, dirty/untracked files,
historical migrations and unrelated package edits were preserved. No resets,
discarding changes, automatic grants, or production/developer DB mutations.

Canonical contracts were updated before coding with the user's approved
permission/precondition/warning decisions. Resume checkpoint:
`docs/e002-f011-progress.md`.

## Implementation map

### E-002

- Application: Abstractions/Catalogue/ICatalogueAuthorizationQueries.cs;
  Models/Catalogue/CatalogueContracts.cs; Dtos/CatalogueDtoMetadata.cs.
- Infrastructure: CatalogueAuthorizationQueries.cs, CatalogueWriteSecurity.cs;
  AuthCookieService and recipe/ingredient persistence adaptations.
- API: CatalogueAuthorization.cs; CatalogueEtags, CataloguePrecondition,
  CatalogueBoundary and CatalogueOperationFilter helpers; AdminController,
  authentication/infrastructure/OpenAPI/CORS/no-store registrations.
- Database: new `migrations/20261012_010_e002_catalogue_permissions.sql`;
  canonical scriptDatabase.sql and regenerated data/generated/sql/01-schema.sql.
  Migrations 001-009 are unchanged.
- Operator: DatabaseMigrator/PermissionOperator.cs and explicit permission CLI;
  separate migration JSON connection, no HTTP/self-service assignment.
- Tests: E002CatalogueSecurityTests; adapted legacy admin, cookie, CSRF and
  recipe-fat tests with explicit synthetic provisioning. Ordinary authenticated
  fixtures never receive permissions automatically.

Admin reads require Catalog.ReadAdmin; writes require Catalog.Write, independently.
Current permissions are queried on every request and checked again under the
actor lock for writes. Active/security-version state revokes stale cookies.
Permission changes record operator/time and advance the account security version.
Resource updates use separate resource_version and exact ETag/If-Match checks;
missing 428, mismatch 412, whole resource/relationships/version/actor/audit atomic.
Admin-only envelopes contain existing resource DTO plus version/actor metadata;
ordinary discovery/favorites/preview/detail serialization remains unchanged.

Operator and deployment details: `docs/e002-catalogue-operations.md`.

### B08

- Added MealPlannerDtos, MealPlannerRowData, MealDefaults, MealPlanNutrition,
  MealPlanReadModelBuilder, IMealPlannerService/MealPlannerService and
  IMealPlannerQueries/MealPlannerQueries.
- MealPlannerSql/MealPlannerReader use one QueryMultiple command with four
  bounded result sets on a coherent read-only REPEATABLE READ transaction.
- GET /api/meal-plan/days?from=YYYY-MM-DD&through=YYYY-MM-DD validates inclusive
  1-7 dates, rejects extra/repeated query fields, never creates settings/days,
  and returns one normalized projection for each date.
- Untouched dates get only Current virtual defaults; saved-empty stays saved
  empty. No Current and Easy have null targets/remaining. Intermediate uses
  stored targets across all dates. Per-serving round2 precedes multiplication;
  empty totals are zero, unknown propagates per nutrient, negative remaining
  is retained. Unavailable recipes remain visible/removable placeholders.
- Warnings recipe_fat_unknown / recipe_unavailable are deduplicated at meal/day
  level. Unavailable recipes do not also get the missing-fat warning.
- Added F011B08ReadModelTests/F011B08PlannerReadTests, including one measured
  application command for both empty and maximum 7x12x20 item ranges.

### B09

- Added strict MealRequests, IMealService/MealService, IMealRepository/
  MealRepository, MealSql and captured PlannerMutationRowData.
- POST /api/meal-plan/days/{date}/meals: 201 plus one-date Location; Current
  default slot is checked under the owner lock on absent dates only, otherwise
  explicit creation; only the selected meal is persisted, max 12, dense order.
- PATCH /api/meal-plan/meals/{mealId:int}: normalized plain name; valid same-name
  update is a no-op with unchanged revision/timestamps.
- DELETE same route: scoped ownership, descendant cascades, one-batch order
  compaction, retain the saved day even after the final meal is removed.
- All writes use existing MealPlanTransaction and X-Meal-Plan-Revision/CSRF;
  scoped validation precedes version conflicts for saved IDs, first settings/
  parents roll back, revision advances once for changes, readback is captured
  and validated before commit. No per-item reads or new B09 schema.
- Added MealPlannerBoundary/schema/operation filters, DI and no-store.
- Added F011B09MealTests for ownership/validation/caps/first-writer races/
  compaction/cascades/retention/no-ops/rollback/CSRF/OpenAPI.

## Verification commands and observed results

All dotnet commands run at Backend root; dataset commands at data/tools.
Database tests explicitly set APPETEE_RUN_DB_INTEGRATION_TESTS=true. The fixture
enforces loopback and exactly appetee_backend_tests before reset-capable access.

- `npm test`: 48/48 passed.
- `npm run validate`: passed, 121 ingredients / 1699 recipes, zero errors and
  2787 existing dataset warnings (not new canonical-data edits).
- `npm run build`: passed; regenerated canonical SQL through the generator.
- `dotnet build Appetee.sln --no-restore --verbosity minimal`: passed, 0 errors,
  0 warnings, 23.36s after final runtime source changes; final rebuild after the
  test-only fixture correction also passed, 0 errors/warnings, 16.63s.
- E002/legacy admin/auth-cookie/CSRF/B01 recipe nutrition filter: 114/114 passed,
  zero failed/skipped, 4m57s before the last small final-hardening/test additions.
- B08 rerun after interruption: 34/34 passed, 56s.
- Expanded B08/B09: 75/75 passed, zero failed/skipped, 2m27s before final culture/
  precondition hardening additions. Saved `.dotnet/test-results/b08-b09-targeted.trx`.
- Final combined targeted filter (E002/legacy Admin/AuthCookie/B01-recipe/E005/
  F011B08/F011B09): 192/192 passed, zero failed/skipped, 9m04s. Saved report:
  `.dotnet/test-results/e002-b08-b09-targeted.trx`.
- Full regression: 885 total, 883 passed, 2 failed, zero skipped; 39.8332 minutes.
  Original report: `.dotnet/test-results/e002-b08-b09-regression.trx`. Failures:
  one MySQL fixture-reset timeout before a test body; one old B02 upgrade fixture
  retained E-002/010 schema while baselining through 008, then hit a duplicate
  constraint during 010. Corrected only that synthetic fixture to remove the
  later schema; migrations and runtime behavior were not weakened or changed.
- Affected rerun: `dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj
  --no-build --no-restore --filter 'FullyQualifiedName~F011B02SchemaTests|FullyQualifiedName~E002CatalogueSecurityTests.Reads_Anonymous401_Ordinary403'
  --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=e002-upgrade-rerun.trx'
  --results-directory .dotnet/test-results`: 9/9 passed, zero failed/skipped, 26s.
  Both original failing cases passed, plus all B02 schema tests. Report retained.
  A fresh whole-suite rerun after this fixture correction was not run.

Development failures are not passes: missing admin grants in two legacy CSRF
fixtures, Dapper UInt16/int sort constructor, reference-based nested-list test
comparison, settings-count expectations, and duplicate normalized model-state
keys. Fixtures/mapping/error handling were corrected without weakening policy.

## Database actions actually run

Only isolated synthetic fixtures: schema/seeds reset by guarded test fixture,
permission grants/revocations, audit fault and meal fault triggers removed in
finally, legacy-column downgrade followed by actual migration 010, EXPLAIN,
maximum-size graph seed and concurrency checks. No developer/production reset,
standalone real migration command, baseline adoption, or real permission grant.

Other named meaningful coverage includes auth/session/profile, discovery/search/
cursor/dietary filtering, favorites and Cooking View through the full run. The
two recorded full-run failures are reported above, not hidden in aggregate claims.

## Remaining limitations

- Migration 010 must be deployed before updated runtime authentication. Local
  appetee's earlier empty migration history needs audited adoption; do not
  blindly replay migrations or rebuild the database.
- Existing tickets without the security-version claim require fresh sign-in;
  nobody is automatically made an administrator.
- E-002 frontend navigation/capability consumption is outside scope. No new
  session-capability response contract was invented.
- Pre-existing AccountClosureSql deletes the user without first removing the
  F-011 settings/day/plan roots. Existing restrictive FKs block account closure
  for accounts with planner data. That separate compatibility issue is not fixed
  by this narrowed work and must not be labelled successful cleanup.
- No B10-B15 or frontend completion/production rollout is claimed. Builds are
  not a fresh vulnerability audit; unrelated existing package changes preserved.
