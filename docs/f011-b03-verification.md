# F-011 backend B03 implementation and verification

## Scope

Backend-only B03 foundation, following `Phase_B03_write_protocol_and_errors.md`.
The repeated B02 reference in the phase-three request was treated as the prior
schema prerequisite, not an instruction to replace the B03 implementation.
Existing B01 recipe nutrition/projection, E-008 migration tooling, B02 schema,
and unrelated working-tree changes were preserved. No migration was recreated
or renumbered. No frontend, later-phase endpoint, or new schema was added.

## Implementation

- `src/Appetee.Application/Models/MealPlans/MealPlanRules.cs`: approved structural
  limits and command/copy deadlines.
- `src/Appetee.Application/Models/MealPlans/MealPlanWriteResult.cs`: persistence-neutral
  captured result; revision serializes as an exact decimal string.
- `src/Appetee.Infrastructure/MealPlans/MealPlanSql.cs`: parameterized owner lock,
  settings lookup/lazy insert, and conditional revision increment.
- `src/Appetee.Infrastructure/MealPlans/MealPlanTransaction.cs`: internal single
  connection/READ COMMITTED transaction; user lock first; optional scoped target
  validation before revision comparison; effective initial revision zero;
  no-op rollback without persistent settings; one increment for changes;
  shared UTC timestamp; readback after increment and before commit;
  cancellation-safe rollback; 15-second commands and 30-second copy deadline;
  at most one known-rollback deadlock retry, never a retry after commit starts.
- `src/Appetee.Api/Helpers/MealPlanRevisionHeader.cs`: strict single nonnegative
  signed-BIGINT decimal header; stable missing/invalid error codes.
- `src/Appetee.Application/Utils/ApiException.cs` and
  `src/Appetee.Api/Controllers/ErrorsController.cs`: optional stable codes/field
  errors in existing ProblemDetails, retaining traceId, statuses, namespace,
  and legacy constructor usage.
- `src/Appetee.Api/Extensions/InfrastructureServiceCollectionExtensions.cs` and
  `src/Appetee.Infrastructure/Properties/AssemblyInfo.cs`: scoped registration
  and access for existing API composition without making the helper public.
- `tests/Appetee.Api.Tests/Unit/F011B03RevisionHeaderTests.cs` and
  `tests/Appetee.Api.Tests/Integration/F011B03WriteProtocolTests.cs`: header,
  serialization, compatibility, lock/race, rollback, cancellation, ownership,
  deadline, retry-bound, revision-exhaustion, readback, and EXPLAIN coverage.

No new Application use case exists in this bounded phase, so
`ApplicationServiceCollectionExtensions.cs` was not changed. The existing single
`TimeProvider.System` registration is reused. Future repositories must supply
their scoped ownership checks and captured readback through this common helper;
their endpoint implementation belongs to later phases.

## Database safety and actions

Tests opted in with `APPETEE_RUN_DB_INTEGRATION_TESTS=true`. The fixture enforces
a loopback MySQL host and the fixed `appetee_backend_tests` database. Only that
isolated fixture resets test tables and runs migration/constraint tests. No
production/developer database deployment, manual rebuild/reset script, new B03
migration, or schema regeneration was run for B03. Existing migrations 008/009
and `data/generated/sql/01-schema.sql` remain intact.

Deadlock retry tests inject server error 1213 using MySQL SIGNAL and verify real
transaction rollback; they are fault-injection tests, not a claim that a naturally
occurring multi-row deadlock was reproduced.

## Verification

Commands run from the backend root, with `DOTNET_CLI_HOME` set to the repository's
`.dotnet` directory and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
dotnet build Appetee.sln
dotnet test tests\Appetee.Api.Tests\Appetee.Api.Tests.csproj --no-restore --filter FullyQualifiedName~F011B03
dotnet test tests\Appetee.Api.Tests\Appetee.Api.Tests.csproj --no-restore
```

- Solution build succeeded with zero errors and six NU1900 warnings: the NuGet
  vulnerability feed was unreachable. Package vulnerability auditing could not
  complete; this is not a clean security-audit result.
- A resumed interrupted full run had 373 passes and one fixture-reset timeout
  in `UsersEndpointsTests.GetMe_ReturnsTheAuthenticatedUser`. A focused rerun
  including that test passed 28/28. This prior failure is not hidden or counted
  as a successful full run.
- B03 verification before the final EXPLAIN test: 31/31 passed, zero skipped.
- Final full suite: 382/382 passed, zero failed/skipped, duration 10m57s. This
  includes the final EXPLAIN test and existing B01/B02, auth, profile, recipe,
  favorites, and migration coverage.
- Final B03 named filter: 32/32 passed, zero failed/skipped, duration 1m11s.
- Final solution build after all test additions: zero errors, six NU1900 warnings.

## Deployment boundary

No new HTTP endpoint was introduced. E-005 CSRF transport/validation remains a
mandatory prerequisite before later exposed state-changing endpoints are
deployed. B03 completion does not imply those endpoints or the whole Meal Plan
feature are complete.
