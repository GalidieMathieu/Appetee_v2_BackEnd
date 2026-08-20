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

Bulk dataset runs use `images:queue` or `build` and leave new image work to the repository owner. `images:source` and `images:related` are retained for explicitly requested dedicated image passes; accepted results still require visual review.

- `generation/acquire-batch-0001.mjs` contains the reproducible first-batch acquisition adapter.
- `generation/add-restriction-diets.mjs` backfills ingredient compatibility and derives Gluten Free/Lactose Free recipe diets.
- `shared/diet-compatibility.mjs` and `shared/meal-categories.mjs` are canonical rules imported by acquisition, generation, and validation.
- `generation/generate.mjs` validates JSON before generating the ordered files in `generated/sql/`, indexes, reports, and image queues. Generated outputs must not be edited manually.
- `generation/candidate-progress.mjs` verifies the immutable candidate-file hash and derives the completed/skipped/unresolved cursor.
- `validation/validate.mjs` independently recalculates nutrition, cost, badges, restriction diets, identities, references, assets, and diversity.
- `image-processing/sync-image-queues.mjs` maintains the two pending image handoff files.
- `image-processing/acquire-source-assets.mjs` attempts exact source/product-page real photos and records honest private/test-use provenance; accepted downloads still require visual dish review.
- `image-processing/acquire-related-recipe-assets.mjs` finds distinct related real-food photographs for pending recipes, supports Bing or DuckDuckGo plus simplified dish-form retries, rejects exact and perceptual duplicates, preserves discovery/original URLs, and records unverified private/test-use provenance.
- `generation/checkpoint.mjs` refuses invalid data and updates all persistent resume metadata.

The application does not need to be started for dataset generation or validation.

`database/` and `deployment/` are reserved integration boundaries. Dataset tools currently generate artifacts only; they do not reset a database or deploy/synchronize Azure resources.
