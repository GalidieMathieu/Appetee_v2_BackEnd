# Resume Prompt

Continue the Appetee bulk development-dataset task from the last valid checkpoint.

Do not restart the dataset and do not regenerate already validated records unnecessarily.

First read:

- root `AGENTS.md`;
- `data/AGENTS.md`;
- `data/DATASET_SPEC.md`;
- `data/workflow/PLAN.md`;
- `data/workflow/RESUME.md`;
- `data/workflow/progress.json`;
- `data/version.json`;
- `data/generated/reports/validation.json`;
- `data/ingredients/index.json` if present;
- `data/recipes/index.json` if present.

Run validation before continuing.

If validation passes, resume from `nextRecipeSequence` and `candidateAcquisition.nextCandidateSequence` in `data/workflow/progress.json`.

Before starting the next batch, inspect current diversity/distribution data and identify the most important gaps.

Continue using the approved diversity-first strategy:

- every intermediate corpus must already be useful;
- preserve approximately 30% student-athlete-oriented recipes;
- preserve approximately 25% Meal Prep progressively so 2,000 recipes reaches at least 500;
- preserve approximately 10% Discovery recipes;
- diversify cuisine, country, protein source, carbohydrate/base, meal timing, meal category (`Main Meal`, `Small Meal`, `Snack`, `Side`, `Meal Component`, `Dessert`, `Drink`), cooking method, prep time, price, nutrition, diets, badges, and source domains;
- process `data/candidates/recipe-names.json` strictly in sequence and document distribution drift;
- reject repetitive recipes that add little new coverage.

Do not invent, reorder, or replace candidate names. Verify every factual field from the matched recipe and canonical rules. Persist completed/skipped/unresolved candidate sequences.

Set both `mealType` and `mealCategory` on every recipe. Do not search for, download, or generate images during bulk growth. Mark new images pending with `aiGenerated: false` and regenerate both `research/image` owner handoff queues.

Classify `mealCategory` by the recipe's actual role. The candidate plan targets approximately 85% `Main Meal` and 15% combined other categories; do not force inaccurate classifications.

For every ingredient, record explicit `dietCompatibility.glutenFree` and `dietCompatibility.lactoseFree` values. Derive the recipe diets `Gluten Free` and `Lactose Free` only when every referenced ingredient qualifies, and maintain useful coverage of both diets in future batches.

Run `npm run images:queue` or `npm run build` so the owner receives complete recipe-name/source-URL and ingredient-name/Walmart-URL lists for everything pending. Reserve `images:source` and `images:related` for an explicitly requested dedicated image task.

Continue with a target of approximately 50 new validated recipes per checkpoint. If the full target is unsafe, finish the largest fully valid partial checkpoint instead.

Do not ask me to repeat information already stored in the repository.

If resources become insufficient to safely finish another batch, follow the mandatory stop-before-failure protocol: validate, regenerate SQL/indexes/reports, update progress/resume files, record current diversity gaps, create a ZIP snapshot with the exact valid recipe count, report exact counts and next sequence, then stop cleanly.
