# E-005 backend CSRF contract

## What this protects

Browsers attach login cookies automatically. Without CSRF protection, an
untrusted website can try to submit changes under a logged-in user's authority.
This slice uses ASP.NET Core's bound antiforgery cookie/request-token pair plus
explicit origin and Fetch Metadata checks before unsafe API execution. It does
not replace authentication, ownership checks, administrator authorization, or
XSS prevention. CORS alone is not mutation protection.

Framework reference: [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

## Chosen topology and wire contract

Keep the existing cross-origin, credentialed cookie topology. Reuse
`Cors:AllowedOrigins` as the exact frontend origin allowlist; same-origin API
requests are also allowed. Do not configure wildcards, URL paths, or prefix
matches. Empty development allowlists now mean same-origin only; production
still refuses an empty allowlist.

`GET /api/auth/csrf` is available both anonymously and after login. It returns
exactly `{requestToken:string,headerName:"X-CSRF-TOKEN"}` and stores an
antiforgery cookie named `__Host-appetee-csrf`: Secure, HttpOnly, SameSite=None,
host-only, Path=/. Secure HTTPS is required, including development. Responses
are no-store. This endpoint creates no Meal Plan settings or other database rows.

All POST/PUT/PATCH/DELETE calls under the exact `/api` path segment require:

- The current matching antiforgery cookie.
- Exactly one nonempty `X-CSRF-TOKEN` header.
- Exactly one trusted `Origin` header, including non-browser API clients.

This includes anonymous login/signup/recovery, logout, uploads, and all current
and future API mutations. JSON, URL-encoded, and multipart bodies have no
exemption. A form-body token does not replace the header. Authorization may
return 401 first for protected resources; no unauthenticated mutation executes.
GET/HEAD/OPTIONS do not require a token. CORS preflight remains supported.
Bootstrap may omit Origin for non-browser clients; a supplied untrusted Origin
is rejected. Browser bootstrap is an API fetch, not an embedded/navigated form.

If Fetch Metadata is supplied, only `same-origin`, `same-site`, or `cross-site`
sites; `cors` or `same-origin` modes; and `empty` destinations are accepted for
mutations/bootstrap. Headers may be absent for older/non-browser clients, but
unsafe requests still require Origin and token. Trusted cross-site SPA fetches
are deliberately accepted; navigation, no-cors, frame destinations, malformed
and multi-valued metadata are not. A same-site hint does not grant origin trust.

Missing/invalid/mismatched tokens return existing ProblemDetails with status
400, `code:"csrf_invalid"`, and traceId. Origin/context rejection returns 403
ProblemDetails with traceId. Validation runs before action execution/body
binding, and failure neither mutates data nor retries a request. No token,
measurement, or identity-specific validation detail is returned or logged by
this slice. Error re-execution is outside the `/api` boundary, avoiding loops.
Auth/profile responses use no-store, including authorization failures.

## Client integration (frontend not implemented here)

1. Fetch `/api/auth/csrf` with credentials before the first unsafe request.
2. Keep `requestToken` in memory and send it using the returned `headerName`,
   only to the exact configured API origin/path boundary. Send credentials too.
   Browsers supply Origin automatically; CLI/native tools must supply it.
3. Fetch a fresh token after login/signup, logout, session expiry/restoration,
   or a switch of account. Tokens are bound to the identity observed at issuance.
4. Do not automatically replay mutations after errors. A confirmed
   `csrf_invalid` rejection is before execution; refresh the token and let the
   caller explicitly decide whether to retry. Other transport failures can have
   an unknown outcome. CSRF validity does not supersede feature revision checks.

Do not deploy this backend contract independently of compatible browser clients:
existing clients without token transport will have mutations rejected. No
frontend files were changed by this implementation.

## Files and verification

Production changes: `src/Appetee.Api/Security/BrowserRequestOriginPolicy.cs`,
`BrowserCsrfMiddleware.cs`, `CsrfOperationFilter.cs`,
`src/Appetee.Api/Models/CsrfTokenDto.cs`, `Controllers/AuthController.cs`,
`Extensions/ApiServiceCollectionExtensions.cs`, and
`Extensions/WebApplicationExtensions.cs`.

Tests: `tests/Appetee.Api.Tests/Integration/E005CsrfTests.cs`,
`Infrastructure/CsrfTestHandler.cs`, `Infrastructure/AppeteeWebApplicationFactory.cs`,
and three alternate-client constructions in `Integration/AuthEndpointsTests.cs`.
Ordinary integration clients now perform real token bootstrap; raw security
clients deliberately omit it when testing rejection. No production security
toggle or test-only validation bypass was added.

Run from the backend root:

```powershell
$env:APPETEE_RUN_DB_INTEGRATION_TESTS = 'true'
$env:DOTNET_CLI_HOME = 'C:\Users\galid\Documents\Appetee\Backend\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build Appetee.sln
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore --filter FullyQualifiedName~E005CsrfTests
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --no-restore
```

Initial security tests failed as expected (bootstrap 404; unprotected logout
204), then passed after implementation. Focused CSRF/auth/profile regression
run: 69/69 passed before the last upload/identity/cache additions. Build succeeded
with zero errors and six NU1900 warnings because NuGet's vulnerability feed was
unreachable. The dedicated run before the final cache test passed 34/34. The
final full backend suite passed 417/417, zero failed/skipped, duration 10m43s.
Final named CSRF filter: 35/35 passed, zero failed/skipped, duration 1m27s.
Final solution build: zero errors, six NU1900 vulnerability-feed warnings.

Tests use only the explicitly opted-in, guarded loopback
`appetee_backend_tests` fixture. No production database action, schema change,
migration, or manual reset/rebuild script was required. All existing F-011
B01/B02/B03 work and migrations were preserved.

## Remaining E-005 work and deployment evidence

This is the backend CSRF prerequisite, not completion of the entire initiative.
Frontend interceptor/token lifecycle, real hostile-origin browser tests,
third-party cookie behavior, public-edge CORS/cookie/cache checks, Data
Protection persistence across instances/slots, session-version revocation,
reauthentication, and broader CSP/hosting work remain separate. Integration tests
prove server behavior with real antiforgery and isolated MySQL, not browser
enforcement or staging deployment readiness. Keep the existing Data Protection
configuration; no new persistence directory, key identity, or cloud resource was
invented. In production, verify shared/protected keys and observed request scheme/
host behind the existing proxy topology before release.
