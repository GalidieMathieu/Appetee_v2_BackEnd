# Appetee Backend — AGENTS.md

## Purpose

Work safely and incrementally in the Appetee backend.

Implement exactly one documented phase per task. The task prompt must provide:

```text
Spec: <path to an F-XXX or E-XXX directory>
Phase: <number>
```

If the spec path or phase cannot be resolved, do not guess the intended work.

## Context Loading

Keep context minimal.

For the supplied spec:

1. Read only the requested phase in `README.md`.
2. Read the same phase in `backend.md` if it exists.
3. Read only decisions, dependencies, or assets explicitly referenced by that phase.
4. Inspect only source files and tests needed to implement that phase.

Do not automatically read:

- other phases;
- `frontend.md`;
- unrelated F-XXX or E-XXX documents;
- the roadmap;
- the full production-readiness audit;
- the whole repository.

Use targeted search/range reads rather than loading large documents in full.

The supplied specification is the source of truth for requested behavior.
This file contains permanent engineering constraints.

## Scope

Implement only the requested phase.

Do not implement later phases, unrelated cleanup, speculative abstractions, or opportunistic refactors.

Do not modify the frontend repository.

If a minimal out-of-phase change is required for compilation, migration, or testing, make only that change and report it.

## Repository Map

- `src/Appetee.Api` — HTTP boundary, routing, authorization metadata, ProblemDetails.
- `src/Appetee.Application` — use cases, validation, business orchestration, contracts.
- `src/Appetee.Infrastructure` — Dapper, MySQL, Blob Storage, authentication infrastructure, external adapters.
- `tests/Appetee.Api.Tests` — backend integration/regression tests.
- `../appetee-docs` — a separate shared writable workspace containing cross-team specifications and documentation.

Treat `appetee-docs` as shared state rather than read-only. When a task defers documentation updates until after implementation or until the user explicitly requests them, do not modify those documents early; preserve concurrent frontend or other contributor changes and update them only at the requested point.

Keep controllers thin. Do not put SQL or substantial business workflows in controllers.

For new code, keep ASP.NET, Azure, Dapper, and MySQL-specific types out of Application logic when practical. Use application interfaces/ports for infrastructure concerns.

## Code Comments and File Metadata

- Add comments only when they explain intent, a non-obvious invariant, a security boundary, or an important tradeoff. Do not restate straightforward code.
- Keep comments accurate when behavior changes; remove stale or redundant comments.
- Every newly created source or test class file must begin with a short header containing its purpose, creation time, and last-updated time. Use ISO 8601 timestamps with an explicit UTC offset.
- Every newly introduced class, record, interface, or enum must have a concise description of what it is used for. A file header may serve as that description when the file contains one primary type and names it explicitly.
- Preserve the original creation timestamp and update the last-updated timestamp whenever the file changes materially.
- Do not add metadata headers retroactively to unrelated existing files unless the task explicitly requests it.

## Security Invariants

Preserve these on every change:

- Treat all client input as untrusted.
- Derive the current user from the validated server principal.
- Never trust client-supplied ownership, role, permission, audit actor, workflow state, nutrition totals, or cost totals.
- Enforce authorization and ownership on the backend.
- Administrative operations require explicit backend authorization; authentication alone is insufficient.
- Never log passwords, cookies, tokens, authorization headers, connection strings, full authentication requests, or unnecessary personal data.
- Preserve antiforgery/request-origin protection for cookie-authenticated state-changing requests. CORS is not CSRF protection.
- Validate server-side lengths, counts, numeric ranges, precision, body sizes, collection sizes, and file limits.
- Use purpose-specific request DTOs and parameterized SQL.
- Keep authoritative nutrition, cost, ownership, permission, and workflow calculations on the server.
- Use versioned forward migrations for schema evolution; do not introduce destructive production rebuild paths.
- Use transactions for related MySQL writes and define conflict behavior for concurrent authoritative updates.
- Treat MySQL and Blob Storage as separate resources; define idempotency/reconciliation/recovery when partial failure is possible.
- For uploads, do not trust extension or declared MIME type; enforce bounded content validation and safe decode/re-encode behavior required by the spec.
- Preserve consistent HTTP status and ProblemDetails semantics without exposing internal exception details.
- Never weaken an existing security control merely to make a phase work.

If the requested spec conflicts with one of these invariants, stop and report the conflict.

## Tests

Tests are part of the phase.

For every code-changing phase:

- implement the tests listed for that phase;
- add the minimum negative/boundary regression tests needed for changed security or failure behavior;
- never delete or weaken a security test just to make the suite pass.

When relevant, cover anonymous, owner, other-user, admin, removed-admin/stale-session, invalid/boundary input, concurrency, replay, cancellation, and dependency-failure cases.

Run targeted tests during development and, when practical before completion:

```bash
dotnet build Appetee.sln -c Release
dotnet test tests/Appetee.Api.Tests/Appetee.Api.Tests.csproj -c Release
```

Run migration, dependency, formatting, or contract checks only when the phase touches those areas.

Report only checks actually run. Distinguish pre-existing failures from failures introduced by the phase.

## Done

A phase is done only when:

- the requested phase is implemented and no later phase was added;
- required tests exist and relevant checks pass, or remaining failures are explicitly reported;
- authorization, ownership, validation, logging, API, migration, concurrency, and recovery behavior remain correct where affected;
- required documentation/contracts for that phase are updated.

Do not mark the entire F-XXX or E-XXX complete unless all of its phases are complete.

## Final Report

Keep the final response concise:

```text
Phase:
Files changed:
Tests/checks:
Security/API/data impact:
Manual verification:
Deferred to later phases:
```
