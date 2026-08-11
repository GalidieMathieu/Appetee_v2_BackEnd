# Dataset Tools

Run commands from this directory:

```powershell
npm install
npm run images:queue
npm run images:source
npm run images:related
npm run validate
npm run build
npm run candidate:status
npm run checkpoint
```

Image acquisition order is `images:source` for exact recipe/product-page photos, then `images:related` for distinct related real-food recipe photos. Visually audit accepted related results. AI is a final fallback only after both real-image methods fail, unless the current user reserves AI work for a separate run.

- `generation/acquire-batch-0001.mjs` contains the reproducible first-batch acquisition adapter.
- `generation/add-restriction-diets.mjs` backfills ingredient compatibility and derives Gluten Free/Lactose Free recipe diets.
- `diet-compatibility.mjs` is the shared allow-list and derivation rule used by acquisition and validation.
- `generation/generate.mjs` validates JSON before generating schema, SQL, indexes, and distributions.
- `generation/candidate-progress.mjs` verifies the immutable candidate-file hash and derives the completed/skipped/unresolved cursor.
- `validation/validate.mjs` independently recalculates nutrition, cost, badges, restriction diets, identities, references, assets, and diversity.
- `image-processing/sync-image-queues.mjs` maintains the two pending image handoff files.
- `image-processing/acquire-source-assets.mjs` attempts exact source/product-page real photos and records honest private/test-use provenance; accepted downloads still require visual dish review.
- `image-processing/acquire-related-recipe-assets.mjs` finds distinct related real-food photographs for pending recipes, preserves discovery/original URLs, and records unverified private/test-use provenance.
- `generation/checkpoint.mjs` refuses invalid data and updates all persistent resume metadata.

The application does not need to be started for dataset generation or validation.
