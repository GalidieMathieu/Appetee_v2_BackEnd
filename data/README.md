# Appetee Development Dataset

This directory contains Appetee's finalized development/mock dataset: 1,699 recipes, 121 canonical ingredients, record-specific AVIF media, deterministic seed SQL, validation, local database bootstrap tooling, and guarded Azure dataset-media synchronization.

## Quick start — build the local database

From the backend repository root in PowerShell:

```powershell
cd data\tools
npm install
npm run db:reset:local
```

`db:reset:local` is the primary dataset command. It:

1. proves the configured target is local and named exactly `appetee`;
2. validates all canonical JSON and AVIF assets;
3. regenerates indexes, reports, and ordered SQL;
4. drops and recreates the local `appetee` database;
5. loads schema, reference, ingredient, and recipe data;
6. verifies counts, relationships, and deterministic image mappings.

The reset is intentionally destructive to the configured local `appetee` database. It accepts only `localhost`, `127.0.0.1`, or `::1`, has no force/remote bypass, and must not run concurrently.

To verify an existing database without changing it:

```powershell
npm run db:verify:local
```

## Deploy dataset media to Azure

Use Node.js 22 or newer and sign in with the Azure CLI. The normal development target is already read from `src/Appetee.Api/appsettings.Development.json`, so no environment variable is required:

```powershell
az login
npm run images:sync:azure -- --dry-run
npm run images:sync:azure
npm run images:verify:azure
```

Always inspect the dry run before the first real synchronization. The sync authenticates through `DefaultAzureCredential`, validates and hashes all canonical assets, lists the existing `dataset/` prefix once, uploads only missing or changed bytes with eight bounded workers, and verifies the result. Remote extras are reported and retained. The tool never creates a container, changes anonymous access, or deletes a Blob.

The configured development target is `https://appeteeimages.blob.core.windows.net/appetee-images-dev`. Its container access was verified as non-anonymous/private on 2026-08-21, and all 3,519 canonical assets were synchronized and verified there. An immediate second sync reported zero uploads and zero updates.

All current dataset media remains private/test-only (`productionApproved: false`). The guard rejects any attempt to synchronize it to an anonymously readable container; there is no override. Use `AzureStorage__AccountUrl` and `AzureStorage__ContainerName` only when you intentionally need a temporary target override.

## Configuration

Database and Azure configuration precedence:

```text
ConnectionStrings__AppeteeDb environment variable
    > src/Appetee.Api/appsettings.Development.json
    > src/Appetee.Api/appsettings.json

AzureStorage__AccountUrl / AzureStorage__ContainerName environment variables
    > src/Appetee.Api/appsettings.Development.json
    > src/Appetee.Api/appsettings.json
```

Temporary PowerShell override:

```powershell
$env:ConnectionStrings__AppeteeDb = "Server=localhost;Port=3306;Database=appetee;User ID=root;Password=your-password;"
npm run db:reset:local
Remove-Item Env:\ConnectionStrings__AppeteeDb
```

The tools never print the password or full connection string.

## Directory structure

```text
data/
├── README.md                  # Operator quick start
├── DATASET_SPEC.md            # Stable canonical contract
├── AGENTS.md                  # Dataset maintenance rules
├── CHANGELOG.md               # Historical dataset record
├── version.json               # Current dataset identity/counts
├── ingredients/               # Canonical ingredient JSON/assets and index
├── recipes/                   # Canonical recipe JSON/assets and index
├── generated/
│   ├── reports/               # Reproducible validation/distribution reports
│   └── sql/                   # Ordered local schema/seed files
└── tools/
    ├── database/              # Guarded reset and read-only verification
    ├── deployment/            # Azure synchronization boundary
    ├── generation/            # Deterministic SQL/index/report generation
    ├── shared/                # Shared rules, Blob names, and configuration
    ├── test/                  # Node tooling tests
    └── validation/            # Complete canonical validation
```

Historical acquisition candidates, research caches/staging images, batch/checkpoint state, old snapshot ZIPs, and one-off migration/acquisition scripts were retired after the mock dataset was finalized. Do not recreate those structures unless a new approved requirement needs them.

## Source of truth

Canonical JSON and record-local AVIF assets under `ingredients/` and `recipes/` are authoritative. Indexes, reports, and SQL are reproducible outputs and must not be edited manually.

The fixed SQL order is:

1. `generated/sql/01-schema.sql`
2. `generated/sql/02-reference.sql`
3. `generated/sql/03-ingredients.sql`
4. `generated/sql/04-recipes.sql`

## Maintenance commands

Run from `data/tools`:

```powershell
npm test
npm run validate
npm run build
npm run db:reset:local
npm run db:verify:local
npm run images:sync:azure -- --dry-run
npm run images:sync:azure
npm run images:verify:azure
```

`db:reset:local` already runs validation and generation. Use the individual commands when changing or auditing the dataset without rebuilding MySQL.

See [`tools/README.md`](./tools/README.md) for command details, [`tools/database/README.md`](./tools/database/README.md) for the database safety contract, and [`tools/deployment/README.md`](./tools/deployment/README.md) for the Azure runbook.

## Media boundary

Every canonical record owns its final AVIF assets. Dataset media retains its recorded provenance and approval state; assets with `productionApproved: false` are private/test-use media and must not be synchronized into an anonymously public container.

## Git

Commit canonical JSON, tooling, indexes, generated SQL/reports, specifications, and version metadata. Record-local AVIF asset folders remain ignored by Git because of their size.
