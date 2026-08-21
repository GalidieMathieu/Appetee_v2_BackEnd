# Appetee Development Dataset — Agent Rules

## Purpose

This directory contains the finalized development/mock dataset used by Appetee. Treat the canonical ingredient and recipe records as stable application inputs, not as an active bulk-acquisition project.

## Source of truth

- `ingredients/*/ingredient.json` and `recipes/*/recipe.json` are authoritative.
- Record-local AVIF files under `assets/` are authoritative media.
- `ingredients/index.json`, `recipes/index.json`, `generated/sql/`, and `generated/reports/` are reproducible outputs.
- Never repair a generated file without repairing its canonical source or generator.

## Required context

Before changing canonical records or dataset tooling, read:

1. `README.md`;
2. `DATASET_SPEC.md`;
3. `generated/reports/validation.json`.

Use the lightweight indexes for lookup instead of loading the full corpus when possible.

## Change boundaries

Preserve the existing 1,699 recipes and 121 ingredients unless the user explicitly requests a canonical-data correction. Do not restart bulk acquisition, reconstruct deleted research queues, or introduce checkpoint/candidate workflows.

Allowed maintenance includes:

- correcting a verified canonical-data defect;
- maintaining validation and deterministic generation;
- maintaining local database bootstrap/verification;
- maintaining deterministic dataset media deployment;
- updating generated artifacts after an approved source change.

Do not independently implement unrelated API, frontend, authentication, recommendation, or meal-planning behavior from this directory.

## Validation and generation

Run from `data/tools`:

```powershell
npm test
npm run validate
npm run build
```

For a local database rebuild:

```powershell
npm run db:reset:local
```

For Azure dataset-media deployment, run a dry run first and use only an existing non-anonymous development container:

```powershell
npm run images:sync:azure -- --dry-run
npm run images:sync:azure
npm run images:verify:azure
```

Never add a public-access, licensing, credential, deletion, or container-creation bypass.

The reset is intentionally destructive only to local hosts with the exact database name `appetee`. Never weaken or bypass that guard.

## Integrity rules

- Stable IDs retain the `ING-####` and `REC-####` formats.
- JSON remains canonical; SQL remains generated.
- Recipe ingredient references must resolve to canonical ingredient seed IDs.
- Nutrition, cost, diet, badge, measurement, and asset validation must continue to pass.
- Deterministic Blob names must remain derived from seed IDs.
- Preserve factual source, product, nutrition, and image provenance.
- Never promote `productionApproved: false` media into an anonymously public storage boundary.

## Completion gate

Any approved maintenance change is complete only after tooling tests, dataset validation, SQL regeneration when applicable, and relevant database/backend verification pass.
