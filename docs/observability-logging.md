# Privacy-safe backend diagnostics

Implemented logging and opt-in API telemetry foundations, 2026-10-16. This is not completion of E-012R, deployed Azure delivery, MySQL tracing, health, browser telemetry, alerts or recovery work. The revised initiative's live evidence/sequencing gates remain open.

## What changed

- Existing `ILogger` calls are retained. A single `PrivacyLoggingProvider` projects events before forwarding them to Microsoft's console and Azure App Service filesystem providers. Default raw console/debug/EventLog providers are removed. No new Blob logging export is enabled.
- Console output is one JSON object per line. Azure's filesystem provider adds its normal prefix around the same safe JSON payload; files start with `appetee-diagnostics-`.
- The Microsoft Azure Monitor distribution is added with export disabled by default. When explicitly enabled, span filtering runs before its exporters; approved numeric metrics have dimensions removed. Its default outer log provider is removed and logs are exported only through the existing inner privacy-safe pipeline. Live Metrics, automatic standard-metric extraction, performance counters and offline export storage remain disabled. Filesystem collection is break-glass support, not the durable production telemetry backend.
- Every completed HTTP request has a stable event, normalized method, route **template**, duration, status and trace ID. Successful requests are Information, expected 4xx responses Warning, client disconnects Information, and server failures Error. `ResponseStatusCode` and `ResponseStarted` distinguish a logical failure from a response that already started.
- `X-Appetee-Trace-Id` is server-generated/from the active W3C activity, not copied from an incoming custom header. `X-Trace-Id` remains a compatibility alias. Both match ProblemDetails `traceId` and are exposed with `ETag` through the existing credentialed CORS policy. Allowed origins are unchanged.
- Startup/lifecycle, handled/fallback HTTP failures, planner reads/writes/retries, catalogue/nutrition outcomes, Blob operations, recovery cleanup/delivery failures and account-cleanup failures have stable event IDs/names. Other legacy events remain classified by category/severity, with only approved count/status/boolean fields retained.
- Meal Planner, Nutrition Plan and Catalogue filters preserve original exceptions internally instead of replacing them without an inner cause. Public 500 details are generic in every environment. Existing expected status/code/field-error contracts remain in place; no mutation or retry policy was changed.
- A bootstrap logger can report failures before application DI/options/Key Vault initialization. If unavailable, a safe JSON stderr fallback is used. Startup failure exits nonzero without allowing the runtime to print the original exception text.
- The standalone database migration CLI also replaces raw exception-message output with bounded JSON containing exception types and numeric database codes only. It does not share or load the web host. Existing command behavior and exit codes are unchanged; no migration was run for this work.

## The privacy boundary

This is an allowlist, not a promise that regexes will discover every possible secret. Do not log "everything": log operation metadata and failure structure, never request contents.

Included fields are timestamp UTC, fixed service/version, stable event ID/name, verified type category, trace/span IDs, normalized method, compiled route pattern, status, finite nonnegative counters/timings, approved boolean flags and closed-set outcomes. Unknown category strings are reduced to `Other`; arbitrary scoped dictionaries and rendered messages are not forwarded. New fields/events must be reviewed and covered by negative tests before admission.

Exception diagnostics contain type, bounded inner-cause graph (eight nodes), numeric MySQL error/Azure status, static classification/remediation hint, bounded method/line frames (twelve per cause, application frames preferred), and a diagnostic fingerprint. They intentionally omit exception messages and `Exception.Data`. Source file paths are not emitted. A SQL mapping error can contain a user's actual database values in its message; retaining the raw message would defeat the privacy boundary.

Never emitted by the application logging pipeline:

- Passwords, bearer/API keys, cookies, antiforgery tokens, reset tokens, connection strings, configuration dumps or identity claims.
- Request/response bodies, form/multipart content, filenames, SQL text/parameters, raw exception messages, arbitrary objects or rendered log messages.
- Query strings, request URLs, concrete route IDs, IP addresses, user/email/account/job/resource identifiers, manual-food names or nutrition profile values.
- Activity baggage/tags, unreviewed scope values, Blob/container names, SAS URLs, email recipients or delivery payloads.

The public ProblemDetails `instance` still follows the existing original-path contract; it is not copied into the safe log. The new logging protocol is not a replacement for the existing durable administrative audit records.

Additional third-party providers, App Service auto-instrumentation, SQL tracing, IIS access logs and dumps are **separate security surfaces**. Do not enable them assuming this application-level filter sanitizes their independent output. Do not add a logging provider after the privacy boundary or export arbitrary scopes/tags to telemetry.

## Finding a failure

1. Reproduce the request once. Record its UTC time and `X-Appetee-Trace-Id` (or compatible `X-Trace-Id`) response header or ProblemDetails `traceId`.
2. Once the reviewed exporter is deployed/enabled, search the target Application Insights resource by `operation_Id`. Until delivery is verified, use the approved temporary **backend** App Service filesystem Log stream to find that trace ID. Source changes alone do not enable Azure delivery.
3. Read `http.exception.handled`, then its `FailureKind`, `RootExceptionType`, `FailureHint`, `FailureFingerprint` and `ExceptionChain`. Join the associated `http.request.completed` and domain events by `TraceId`.
4. Inspect the recorded application method/line and the matching deployed build. Share only the required diagnostic record, not the whole log archive.

Example shape (illustration, not a diagnosis of the current production failure):

```json
{
  "SchemaVersion": 1,
  "EventName": "http.exception.handled",
  "Level": "Error",
  "Route": "/api/meal-plan/days",
  "HttpMethod": "GET",
  "StatusCode": 500,
  "TraceId": "<32 hexadecimal characters>",
  "RootExceptionType": "MySqlConnector.MySqlException",
  "FailureKind": "database.column_missing",
  "FailureHint": "Compare deployed query columns with the migrated schema.",
  "ExceptionChain": {
    "Type": "Appetee.Application.utils.InternalServerException",
    "Inner": [{ "Type": "MySqlConnector.MySqlException", "DatabaseErrorNumber": 1054 }]
  }
}
```

Common classifications:

| Failure kind | First check |
| --- | --- |
| `database.column_missing` / `database.table_missing` | Actual runtime database target, deployed query/schema and migration history. Table names alone are not proof of compatible columns. |
| `database.access_denied` | Runtime identity privileges; do not copy credentials into logs. |
| `database.unique_constraint` / `database.foreign_key` | Operation contract, integrity and write ordering. |
| `database.lock_timeout` / `database.deadlock` | Contention and rollback-safe retry policy; never blindly replay an uncertain write. |
| `data.mapping` | Query aliases, CLR types and nullability at the application frame. |
| `dependency.azure` / `dependency.network` / `dependency.timeout` | Provider status, managed identity, firewall/DNS and latency. |
| `security.csrf_rejected` | Origin, cookie/token lifecycle and browser request context, without recording values. |
| `request.conflict` | Refresh/review confirmed state; do not attach a newer revision and replay. |
| `application.failure` | Preserved exception types and application frames. A classification is a diagnostic lead, not proof of the root cause. |

## Azure operator setup — requires live verification

Deploy the backend build; local source changes do not update Azure. For Windows App Service, enable **Application logging (Filesystem)**. Error captures failures; Information additionally captures successful request/domain outcomes. Save, reopen the settings to confirm they persisted, then reopen Log stream. Linux/container operators should enable filesystem/console collection for their hosting mode.

Normal application settings remain `Logging:LogLevel` (environment notation `Logging__LogLevel__...`). Sink-specific rules still apply inside the provider pipeline. Do not enable Verbose indiscriminately or disable application failure logging. Microsoft documents [App Service diagnostics](https://learn.microsoft.com/en-us/azure/app-service/troubleshoot-diagnostic-logs) and the [Azure logging provider](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/providers#azure-app-service).

The pasted incident stream contained **IIS detailed-error HTML**, including a request URL and physical path, rather than a managed exception record. Application code cannot sanitize independently generated IIS pages. For normal operation, turn **Detailed error messages** and **Failed request tracing** off unless an approved, short-lived incident procedure requires them. Application filesystem logging is an incident tool: keep it available during the incident, then disable it after durable Azure Monitor delivery is proven. Do not turn production into Development. If a failure occurs before managed code can start, use restricted platform/startup diagnostics with minimal retention, not public detailed error pages.

The stock Azure file sink is deliberately bounded: 10 MiB per daily file, five retained files, queue capacity 1000, batches of 32, flush period one second. These are diagnostic buffers, not guaranteed audit persistence: queued events can be lost on abrupt termination/overflow, and a full daily file stops receiving further records. Confirm quota, expected volume and retention with the service owner. Console/exporter collection and E-012's future monitoring/storage work must address longer-term reliability, alerting and volume requirements.

Restrict log access using Azure RBAC, apply an operational retention policy, and review exports before sharing. No cloud settings, logging storage account, permissions, alerts or deployment were changed by this implementation.

## Verification actually performed

```powershell
dotnet build Appetee.sln --configuration Release --no-restore --nologo
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~Appetee.Api.Tests.Unit" --nologo --logger "console;verbosity=minimal"
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~Appetee.Api.Tests.Diagnostics" --nologo --logger "console;verbosity=minimal"
```

- Release solution build: passed, zero warnings/errors.
- Backend unit and database-free TestServer suite: 335 passed, zero skipped. The separately filtered, fresh-process `Diagnostics` exporter integration smoke: 1 passed, zero skipped. The original 323-test logging baseline passed before this revision. Coverage includes original-cause preservation across three filters, classified MySQL codes, canary redaction, untrusted scopes/categories/formatters/objects, bounded aggregates, startup/failing-provider fallback, concurrent correlation scopes, generic public 500, trace headers/ProblemDetails, CORS exposure, 200/400/401/404 behavior, client cancellation, standalone migration failure privacy, opt-in/development/release configuration, resource minimization and approved span/metric filtering.
- Controlled local process startup with Production environment, Key Vault and telemetry export explicitly disabled and a deliberately invalid synthetic CORS origin: expected exit 1, structured `app.startup.failed` and `ExitCode: 1`, canary absent. Failure occurs before infrastructure registration/server startup; no DB or Azure service operation is performed.
- Initial test attempts exposed a missing routing-pattern import, incomplete MVC action metadata in fixtures, and a test that reused an exception canary as the deliberately reflected `instance` path. Fixtures were corrected with distinct path/secret canaries and explicit assertions that neither enters the logs; final tests passed. No test timeouts or privacy assertions were relaxed.
- A database-free `Diagnostics` smoke now runs the actual Microsoft distribution/batch exporter through an in-memory Azure transport. It checks real API failure -> response/ProblemDetails/log/export correlation, compiled route preservation, release metadata, safe dependency status, query/baggage/status-message suppression and dropping immutable event payloads. No Azure ingestion request is sent. This is exporter integration evidence, not deployed Azure delivery or live MySQL/Blob integration.
- Initial mixed-host exporter tests exposed TestServer response/activity completion timing and process-wide instrumentation interaction with other TestServer/provider fixtures. The smoke now uses a single distro host, waits for server activity completion and runs in its own fresh filtered test process. Privacy assertions were not removed, tests were not skipped, and the existing suite was not globally serialized. An early import error and fixture nullability warnings were corrected before final verification.
- No live MySQL integration/reset/migration tests, deployed Azure filesystem/ingestion smoke, frontend tests or production deployment were performed. The original production planner failure is **not** declared fixed; deploy and reproduce to obtain its classified cause.

## E-012R configuration and follow-up

Checked-in `Observability:Enabled` is false. To export, the target slot must supply `Observability__Enabled=true`, `APPLICATIONINSIGHTS_CONNECTION_STRING` and an explicit `Observability__ReleaseVersion`. Development is denied export unless `Observability__AllowDevelopmentExport=true` is additionally set for an approved non-production destination. No connection string or actual cloud destination is committed here.

Resource metadata is rebuilt from a fixed service/namespace, bounded release and closed-set environment; arbitrary `OTEL_RESOURCE_ATTRIBUTES`, baggage/tags and host identifiers are not copied. Both distribution URL-query-redaction switches are explicitly set false for **disable-redaction**, so redaction stays enabled; span allowlisting additionally removes URL/host/query values entirely. Initial code configuration explicitly selects 100% traces and retains logs regardless of trace sampling. Operators must review external sampler overrides, caps, cost and retention before enabling export.

The immediate slice's next gate is a real production request searchable in Application Insights by trace ID and deployed version. MySqlConnector instrumentation remains unsubscribed until that gate and its privacy verification; health endpoints and alerts follow per the revised spec. The approved later health policy is read-only anonymous minimal GET/HEAD 200/503, total 3-second readiness budget with shared 15-second cache, and process-only frontend health. No health endpoint is claimed implemented.

The E-012R initiative's `operations.md` contains environment inventory, configuration, KQL/workbook/alert artifacts and nine incident/recovery runbooks. Notification ownership, thresholds, operational retention/SLO/RPO/RTO acceptance, live dependencies, cloud configuration, browser telemetry and measured restore exercises remain pending. Existing E-001 account-closure and E-002 revocation semantics must not be undone during a restore.

## Main implementation files

`src/Appetee.Api/Diagnostics/` owns the privacy projection, sinks, exception snapshot, request correlation, bootstrap fallback, opt-in telemetry settings, resource metadata and span/metric filtering. `Appetee.Application/Diagnostics/LogEvents.cs` owns stable value-free event identifiers. Host/pipeline/error-controller wiring and three exception boundary filters are updated. Existing planner, Blob, recovery and cleanup call sites gain stable event IDs/preserved causes only; their business, persistence, authorization and retry rules are unchanged. The standalone migrator has a separate minimal privacy-safe failure writer. Tests live in `PrivacyLoggingTests.cs`, `RequestDiagnosticsTests.cs`, `MigrationFailureDiagnosticsTests.cs` and `TelemetryPrivacyTests.cs` under the database-free Unit suite, plus `Diagnostics/TelemetryDistroPipelineTests.cs` in the separately run fresh-process category. API package/configuration/host/CORS wiring includes the exporter and header additions; no frontend files changed.
