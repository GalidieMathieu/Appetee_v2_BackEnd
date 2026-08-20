# Dataset Package Changelog

## 0.6.0 — Data workspace reorganization

- Preserved all canonical ingredient and recipe JSON records while separating candidates, workflow state, fixtures, generated artifacts, research, and tools into explicit top-level responsibilities.
- Consolidated ordered SQL in `generated/sql/`, reports in `generated/reports/`, image provenance in `generated/manifests/`, and historical checkpoint ZIPs in `generated/snapshots/`.
- Moved the ordered recipe-name plan to `candidates/recipe-names.json`, workflow state to `workflow/`, and shared validation/generation rules to `tools/shared/`.
- Integrated the optional Wikimedia fallback under `tools/image-acquisition/wikimedia/` as a staging workflow outside normal npm generation.
- Updated generation, validation, image, checkpoint, documentation, and ignore paths without changing canonical record semantics or database behavior.

## 0.5.2 — Complete current recipe-image assets

- Filled all 529 pending recipe image pairs with visually audited real-food photographs: 8 acceptable exact-source photos and 521 related photos found through independent Bing and DuckDuckGo searches.
- Rejected exact-source logos/non-food metadata, 117 polluted or mismatched related results, and later near-duplicate assignments instead of accepting them merely to empty the queue.
- Added independent-provider and simplified dish-form retries, rejected-URL persistence, faster bounded AVIF encoding, and perceptual-hash duplicate protection to the reusable related-image workflow.
- Produced distinct 1200×800 main and 480×320 card AVIF assets for every new recipe; no AI images were generated, and the 529-image batch has zero exact-hash or perceptual near-duplicate pairs.
- Completed all 1,699 recipe asset pairs and all 121 ingredient assets, emptying both `research/image` handoff queues.
- Preserved private/test-only provenance and unverified redistribution status, regenerated manifests, SQL, indexes, distributions, validation state, progress, version, and resume metadata, and retained zero validation errors.

## 0.5.1 — Complete current ingredient-image assets

- Downloaded and visually reviewed the 18 pending ingredient photographs from their Walmart product listings for repository-owner-authorized private test use.
- Replaced the stale 404 source for ING-0106 Whole-Wheat Pita Bread with Walmart's current Papa Pita whole-wheat listing while preserving the original independent price snapshot.
- Converted every acquired asset to a centered 256×256 AVIF below 40 KB and recorded honest source-page, original-image, usage, and production-approval provenance.
- Completed all 121 ingredient assets with unique image hashes and emptied `research/image/ingredients.json`; the 529 pending recipe images were not downloaded or changed.
- Regenerated image manifests, queues, SQL, indexes, distributions, validation state, progress, version, and resume metadata with zero validation errors.

## 0.5.0 — Exhausted ordered-candidate checkpoint

- Added 529 validated recipes as REC-1171 through REC-1699 while processing every remaining immutable candidate sequence from 1965 through 2500.
- Continued past unsupported candidates and recorded seven new unresolved names instead of inventing or reordering replacements; the 2,500-name candidate plan is now exhausted.
- Added 18 researched canonical Walmart/USDA ingredient records, bringing the corpus to 121 ingredients.
- Preserved truthful role classification across Main Meal, Small Meal, Snack, Side, Meal Component, Dessert, and Drink; progressive targets now report 26.8% student-athlete, 25.9% Meal Prep, and 9.5% Discovery.
- Left all new images pending for owner handling and rebuilt `research/image/recipes.json` with 529 recipe-name/source-URL entries plus `research/image/ingredients.json` with 18 ingredient-name/Walmart-URL entries.
- Regenerated SQL, indexes, distributions, image manifests, validation state, progress, plan, version, and resume metadata with zero validation errors.

## 0.4.2 — Complete recipe-image assets

- Filled every previously pending recipe image with a distinct real food photograph, preferring exact recipe-source images and then related web photographs; no AI images were generated.
- Converted all recipe assets to validated 1200×800 main and 480×320 card AVIF variants and preserved honest private/test-only provenance, including source pages, discovered image URLs, and unverified redistribution status.
- Completed all 1,170 recipe asset pairs with distinct primary-image hashes and emptied `research/image/recipes.json`.
- Added repeatable related-image acquisition with candidate skipping, configurable concurrency, format/size rejection, duplicate protection, and resumable manifest recovery.
- Updated the persistent image workflow to use exact source photos first, related real-food photos second, and AI only as the documented final fallback.
- Regenerated image manifests, queues, SQL, indexes, distributions, validation state, progress, plan, version, and resume metadata.

## 0.4.1 — Complete Walmart ingredient-image assets

- Downloaded and visually reviewed the 56 previously pending ingredient images from exact Walmart product listings for repository-owner-authorized private test use.
- Replaced the stale 404 image source for ING-0069 Pork Tenderloin with the current Walmart Tyson two-piece pork tenderloin page and its Walmart-hosted photograph; the original independent price record was not changed.
- Converted every new source image to a centered 256×256 AVIF at or below 40 KB and recorded honest, non-production-approved Walmart provenance in each ingredient JSON record.
- Completed all 103 canonical ingredient assets with 103 distinct hashes and zero pending ingredient-image queue entries; recipe assets were not changed.
- Updated the source-acquisition tool to use browser-compatible Walmart requests, actual acquisition timestamps, configurable concurrency, and explicit exact-source overrides for stale product pages.
- Regenerated image manifests, handoff queues, SQL, indexes, distributions, validation state, progress, plan, version, and resume metadata.

## 0.4.0 — One-thousand-recipe ordered checkpoint

- Added 1,000 validated recipes as REC-0171 through REC-1170 while processing ordered candidate sequences through 1964.
- Continued past unsupported candidates instead of stopping early: 899 new candidates were recorded unresolved, bringing the persistent unresolved total to 905; no new exact-name duplicate was forced into the corpus.
- Required every adaptation source to retain the same primary-protein group plus the candidate's dish form, carbohydrate base, or compatible cooking method; retained researched source ingredient structures and measurements where unchanged.
- Added canonical Walmart/USDA records for fresh green cabbage, fresh cucumber, frozen cauliflower, and dry egg noodles.
- Reached 1,170 recipes and 103 canonical ingredients with zero validation errors; similarity warnings remain advisory for differently named recipes with overlapping canonical ingredient sets.
- Changed the persistent workflow so Codex no longer searches for, downloads, or generates images. Rebuilt the owner handoff queues, including 1,142 pending recipes in `research/image/recipes.json` with recipe names and source URLs.
- Regenerated SQL, indexes, distributions, validation state, progress, plan, version, and resume metadata.

## 0.3.7 — Fifty-recipe ordered checkpoint

- Added 50 validated recipes as REC-0121 through REC-0170 while processing candidate sequences 9 through 65 in order.
- Skipped two semantic duplicates and recorded five additional unresolved candidates without ending the batch.
- Added canonical Walmart/USDA records for fresh kale, dry orzo, and fresh basil, including conservative gluten compatibility for wheat orzo.
- Regenerated SQL, indexes, distribution reporting, validation state, image queues, progress, plan, and resume metadata.
- Preserved the non-AI image workflow; all 53 new image needs remain explicitly queued after exact-source acquisition produced no verifiable downloads.

## 0.3.6 — Ordered candidates and real-photo workflow

- Adopted the immutable ordered recipe-name source now located at `candidates/recipe-names.json` and persisted its SHA-256 plus completed/skipped/unresolved cursor state.
- Added candidate metadata to new recipe JSON and lightweight indexes, with validator enforcement from REC-0117 onward.
- Added four valid candidate recipes and three canonical ingredients; processed candidates 1-8 with three semantic-duplicate skips and one unresolved candidate.
- Changed the candidate-plan meal-category target to approximately 85% Main Meal and 15% combined other roles.
- Enforced real source-photo-first acquisition with honest private/test-use provenance and no AI fallback; unobtrusive photographer credit watermarks are allowed.
- Added three new real recipe photos and kept the remaining new images explicitly pending.

## 0.3.2 — Expanded meal-category roles

- Replaced the legacy `Full Meal`/`Snack` vocabulary with seven canonical meal categories.
- Semantically reclassified all current recipes as Main Meal, Small Meal, Snack, Side, Meal Component, Dessert, or Drink.
- Added a progressive mature-corpus target of approximately 80% Main Meal and 20% combined other roles.
- Added validation and distribution reporting without forcing exact ratios at intermediate checkpoints.

## 0.3.1 — Gluten-free and lactose-free diet derivation

- Added canonical `Gluten Free` and `Lactose Free` diet seed values.
- Added explicit gluten-free and lactose-free compatibility metadata to every canonical ingredient.
- Derived both restriction diets from all referenced ingredients rather than recipe-name assumptions.
- Added validator enforcement so existing and future recipes cannot carry incorrect restriction-diet tags.
- Updated acquisition, generation, checkpoint, and resume instructions to maintain meaningful future coverage.

## 0.2.3 — Meal category and ChatGPT image queues

- Added required `mealCategory` values `Full Meal` and `Snack` independently from `mealType`.
- Classified all 25 current recipes as `Full Meal` and added both fields to future API examples and recipe indexes.
- Added meal-category validation and distribution reporting.
- Added `research/image/recipes.json` and `research/image/ingredients.json` as generated missing-image handoff queues.
- Updated the contract so missing images may remain explicitly pending and are not generated through Codex.
- Added `npm run images:queue` and made normal dataset generation synchronize both queues automatically.
- Limited source acquisition to pending records and exact documented source/product pages; removed broad image search from the normal tool workflow.

## 0.2.2 — Record-specific image and SQL corrections

- Replaced the shared fallback image with 25 distinct recipe images and 47 distinct ingredient images.
- Added per-record test-only source provenance and a consolidated image source manifest.
- Added duplicate-image hash validation and made the image audit preserve existing assets.
- Changed every generated recipe image blob name to `recipes/96ef8a25a7f4433e936a40e6aa6e33c0.avif`.
- Replaced generic `Step N` instruction titles with concise sentence-style titles.
- Validated 25 recipes and 47 ingredients with zero errors and zero warnings.

## 0.2.0 — Diversity-first acquisition

- Added diversity-first acquisition strategy.
- First 100/250/500 recipes must already be representative and useful.
- Added progressive student-athlete, Meal Prep and Discovery targets.
- Added gap-driven batch acquisition.
- Added protein, meal-type, cooking-method, prep-time, cost, calorie and protein distribution tracking.
- Added milestone reviews at 100, 250, 500, 1,000, 1,500 and 2,000 recipes.
- Updated start and resume prompts.
- Preserved mandatory stop-before-resource-failure behavior.
