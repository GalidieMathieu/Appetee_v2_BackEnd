# Dataset Tools

Run commands from `Backend/data/tools`.

## Primary workflow

```powershell
npm install
npm run db:reset:local
```

The reset validates and regenerates the dataset before dropping anything, recreates the local database from the fixed SQL files, and runs read-only verification afterward.

It accepts only `localhost`, `127.0.0.1`, or `::1` when the database name is exactly `appetee`. Remote hosts, other database names, and bypass arguments such as `--force` are rejected before SQL. Do not run two resets concurrently.

Configuration is read from `ConnectionStrings__AppeteeDb`, then `src/Appetee.Api/appsettings.Development.json`, then `src/Appetee.Api/appsettings.json`. Output contains only the redacted `host:port/database` target.

## Commands

```powershell
# Install pinned tooling dependencies
npm install

# Test the tooling without changing canonical data or MySQL
npm test

# Validate canonical JSON, calculations, relationships, and AVIF assets
npm run validate

# Validate, then regenerate indexes, reports, and SQL
npm run build

# Destructively rebuild only the guarded local appetee database
npm run db:reset:local

# Verify the existing local database without changing it
npm run db:verify:local

# Read-only Azure preflight and synchronization plan
npm run images:sync:azure -- --dry-run

# Upload missing/changed dataset assets, then verify
npm run images:sync:azure

# Verify Azure dataset assets without changing them
npm run images:verify:azure
```

`db:reset:local` already runs `validate` and `build`. The individual commands are useful for dataset/tooling maintenance that does not need a MySQL rebuild.

## Maintained modules

- `validation/validate.mjs` validates the complete stable dataset.
- `generation/generate.mjs` produces indexes, distribution/validation reports, and the four ordered SQL files.
- `shared/diet-compatibility.mjs` and `shared/meal-categories.mjs` own reusable classification rules.
- `shared/blob-names.mjs` owns deterministic dataset Blob names.
- `shared/tool-config.mjs` owns BOM-safe layered configuration without exposing secrets.
- `database/mysql-local.mjs` owns connection parsing, the immutable safety guard, and fixed SQL execution.
- `database/reset-local.mjs` orchestrates the explicit destructive local reset.
- `database/verify-local.mjs` performs reusable SELECT-only verification.
- `shared/dataset-assets.mjs` builds the contained, hashed canonical AVIF inventory.
- `deployment/azure-blobs.mjs` owns Azure clients, prefix listing, classification, upload metadata, access safety, and bounded workers.
- `deployment/sync-azure-images.mjs` provides dry-run and idempotent synchronization with automatic post-sync verification.
- `deployment/verify-azure-images.mjs` provides separate read-only Azure verification.
- `test/` covers deterministic naming, generation, configuration, local/Azure safety, workflow ordering, hashing, classification, and concurrency.

Historical batch generators, acquisition adapters, image staging, candidate cursors, and checkpoint tooling were retired with the finalized mock dataset.

Azure media synchronization remains separate from database commands. It requires Node.js 22+, `DefaultAzureCredential` (normally `az login` locally), an existing container, and Blob data access. The normal target comes from development/base appsettings; environment variables are optional overrides. See `deployment/README.md`. The configured `appetee-images-dev` container was verified private with all 3,519 assets present on 2026-08-21.
