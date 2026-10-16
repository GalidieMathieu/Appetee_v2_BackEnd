# E-002 current-session capability handoff — 2026-10-12

User-approved additive session retrieval contract; no frontend application code
changed. Existing interrupted E-002/F-011/CSRF work and migrations are preserved.

## Implementation

- `UserSessionDto` retains identity fields and adds non-null `Permissions`, serialized
  as `permissions`, with required-array OpenAPI metadata.
- `AuthController` maps live server grants onto the session DTO, rejects all query
  parameters, retains existing authentication errors and declares 200/400/401.
- `ICatalogueAuthorizationQueries` / `CatalogueAuthorizationQueries` add a bounded,
  cancellable single-statement permission read. Check active/security version in
  that read to distinguish ordinary/empty from invalid/stale sessions.
- No permission assignment, automatic grant, schema change, migration modification,
  or new catalogue endpoint. Existing no-store middleware covers all statuses.
- `E002SessionPermissionsTests` covers independent grants, exact wire shape,
  ordinary/no side effects, forbidden selectors, disabled/stale/forged tickets,
  revocation/re-login, query-time security validation and generated OpenAPI.
  Existing auth-cookie fake implements the new query contract.
- E-002 README/backend/frontend contracts and the operator API handoff are aligned.
  The frontend prompt now targets the implemented session response and requires
  deployment verification, guards, identity cleanup, ETags and meaningful tests.

## Verification checkpoint

`dotnet build Appetee.sln --no-restore --verbosity minimal`: passed, 0 warnings,
0 errors, 19.83 seconds.

Scoped regression passed: 107/107, zero failed/skipped, 4m18s. This includes all
15 new current-session integration cases and the existing E-002 catalogue,
authentication and CSRF coverage. TRX result counters were inspected. Exact
command from the Backend directory:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS='true'
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~E002SessionPermissionsTests|FullyQualifiedName~E002CatalogueSecurityTests|FullyQualifiedName~AuthEndpointsTests|FullyQualifiedName~AuthServiceTests|FullyQualifiedName~AuthCookieServiceTests|FullyQualifiedName~E005CsrfTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=e002-session-permissions.trx' --results-directory .dotnet/test-results
```

Database actions are restricted to the explicitly opted-in guarded loopback
`appetee_backend_tests` fixture. Tests reset/seed that isolated database and use
synthetic accounts/grants. The existing catalogue suite exercises migration 010
in that fixture. No developer/production migration or account grant is performed.
No fresh full-suite run or frontend/browser verification is claimed by this work.

Result artifact: `.dotnet/test-results/e002-session-permissions.trx`.
Scoped whitespace checks passed; Git reports only existing CRLF normalization
notices. The scoped test process completed successfully; no verification process
from this extension remains running.
