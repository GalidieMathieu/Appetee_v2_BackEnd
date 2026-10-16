# E-002 catalogue permissions and versions

Backend implementation/verification checkpoint is in e002-f011-progress.md.
Canonical contracts live in appetee-docs under E-002/backend.md. Frontend work
is not included. Do not label the whole cross-stack E-002 initiative complete.

## Deployment and migration

New additive migration: migrations/20261012_010_e002_catalogue_permissions.sql.
Production/bootstrap representation: data/generated/sql/01-schema.sql, generated
from scriptDatabase.sql. Existing migrations 001-009 remain unchanged.

Use the existing separate migration JSON connection and audited migration
workflow in database-migrations.md. Back up first; MySQL DDL is not rollback-safe.
An existing DB with empty history must be audited/adopted before normal migrate;
do not blindly replay all historical scripts. No automatic startup migration.

No account receives catalogue permissions in migration or seed. Existing
catalogue records receive version 1 and null actors. Existing cookies without
the new account security-version claim require sign-in after deployment. Runtime
code requires migration 010's schema before startup/authentication is exercised.

## Trusted assignment and emergency revocation

Use only a trusted, authenticated operator with access to the separate migration
credential. Operator reference identifies the person/service conducting the
change, not the target user. Never expose these commands through HTTP or give
application users migration credentials. Verify target account ID out of band.

```powershell
dotnet run --project src/Appetee.DatabaseMigrator -- permission grant <account-id> Catalog.ReadAdmin '<operator-reference>'
dotnet run --project src/Appetee.DatabaseMigrator -- permission grant <account-id> Catalog.Write '<operator-reference>'
dotnet run --project src/Appetee.DatabaseMigrator -- permission revoke <account-id> Catalog.Write '<operator-reference>'
dotnet run --project src/Appetee.DatabaseMigrator -- permission revoke <account-id> Catalog.ReadAdmin '<operator-reference>'
```

Optional final argument is the trusted actor account ID, if the operator is also
an application account. Otherwise actor_user_id is null but actor_reference is
mandatory. Direct permissions have independent meanings: read does not grant
write, write does not grant read. Grant both explicitly when both are needed.

Each actual change locks the target user, changes permission, increments its
security version and writes an administrative_audit row atomically. Old cookies
are revoked; the target signs in again. Repeating the same desired state is a
no-op, not an additional grant/audit/version change. Revocation takes effect on
the next request, with no permission cache; an already serialized write may
complete before the revocation gets the same user lock. Emergency disable is
trusted account-state maintenance, not a new user-facing endpoint.

Audit permission and catalogue changes by resource_type/resource_id and id.
Preserve audit rows through account deletion. Review grants and audit operators
as part of normal deployment/access reviews; do not log request bodies or tokens.

## Admin API handoff

`GET /api/auth/session` now returns `{userId,username,permissions}`. Permissions
are a non-null string array read from current durable grants, never client input
or cookie role/permission claims. Ordinary accounts return `[]`; `Catalog.ReadAdmin`
and `Catalog.Write` remain independent. No query parameters are accepted (`400`),
and stale/disabled/anonymous sessions return `401`. Session responses are no-store.
Deploy this additive API change before the permission-aware frontend, which must
clear user/admin state on identity changes and session/permission loss. A capability
response is a UI hint, not authority; every admin request remains independently
authorized. No new migration is needed for this session extension; migration 010
from the original E-002 work must already be applied through the trusted rollout.

Reads require Catalog.ReadAdmin. POST/PUT catalogue mutations require Catalog.Write
and existing CSRF. GET admin ingredient/recipe detail returns a resource envelope
`{resource,resourceVersion,createdByUserId,updatedByUserId}` and ETag header. The
inner resource retains its existing DTO; ordinary recipe/detail/discovery/
favorites/preview serialization does not gain catalogue metadata.

For an existing recipe update, send the exact read ETag in If-Match. Missing is
428/catalog_precondition_required; mismatched/weak/wildcard/multiple/malformed
is 412/catalog_precondition_failed. Stale updates write nothing. Obtain a fresh
admin read and review before an explicit new update; no blind replay. Successful
create/update returns the resulting ETag and version/actor envelope. CORS exposes
ETag. Catalogue resource_version is separate from X-Meal-Plan-Revision.

No ingredient-update endpoint or self-service permission API was invented.
