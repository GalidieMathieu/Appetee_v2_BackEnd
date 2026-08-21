# Azure Dataset Media Deployment

These commands synchronize already-built canonical AVIF assets under the managed `dataset/` Blob prefix. They are separate from local database reset and runtime/admin media handling.

## Prerequisites

- Node.js 22 or newer and `npm install` completed in `data/tools`.
- An existing Azure Storage account and existing non-anonymous development container.
- `Storage Blob Data Contributor` at the narrowest practical scope.
- A developer credential available to `DefaultAzureCredential`; use `az login` locally.
- `AzureStorage:AccountUrl` and `AzureStorage:ContainerName` in development/base appsettings, or equivalent environment-variable overrides.

The command does not create a container, grant RBAC, change public access, or accept account keys/SAS secrets. Account URLs containing credentials or query strings are rejected.

## First deployment

```powershell
cd data\tools
az login
npm run images:sync:azure -- --dry-run
```

The committed development configuration currently selects `https://appeteeimages.blob.core.windows.net/appetee-images-dev`. To intentionally test another existing container, set `AzureStorage__AccountUrl` and/or `AzureStorage__ContainerName` for that shell; environment values take precedence over appsettings.

Confirm the account, container, non-anonymous access, 3,519 expected assets, and missing/changed counts. Then run:

```powershell
npm run images:sync:azure
npm run images:verify:azure
npm run images:sync:azure
```

The immediate second sync must report `Uploaded: 0`, `Updated: 0`, and `Failed: 0`.

## Safety and behavior

- `--dry-run` performs Azure reads and planning only.
- All local JSON and AVIF assets pass full dataset validation before Azure access.
- Local paths must remain inside their canonical record directory and resolve to regular `.avif` files.
- Missing/changed decisions use SHA-256, seed ID, asset type, and `Content-Type: image/avif`.
- Dataset-version metadata is audit-only and does not trigger a byte upload by itself.
- Upload concurrency is bounded at eight; failures are collected and reported by Blob name.
- Successful synchronization runs read-only verification automatically.
- `images:verify:azure` never writes.
- Remote extras beneath `dataset/` are reported but never deleted; blobs outside that prefix are ignored.
- Unapproved media cannot be synchronized to a container with anonymous Blob/container access, and there is no bypass.

## Verified development target

On 2026-08-21, the workflow authenticated to `https://appeteeimages.blob.core.windows.net` and verified `appetee-images-dev` as non-anonymous/private. The dry run classified all 3,519 canonical assets as unchanged with no extras. Normal synchronization uploaded 0, updated 0, skipped 3,519, and passed automatic verification; the separate read-only verifier also passed. An immediate second synchronization again uploaded 0 and updated 0.

This is a development/test media boundary. Direct Blob URLs are deterministic, but private-container delivery to unauthenticated clients remains part of E-006 rather than this dataset deployment workflow.
