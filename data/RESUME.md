# Dataset Resume State

Checkpoint v0.4.2 is valid and resumable. The corpus is intentionally partial.

## Current state

- Completed recipes: 1170
- Completed ingredients: 103
- Last completed recipe: REC-1170
- Next recipe sequence: 1171
- Next ingredient sequence: 104
- Last candidate processed: 1964
- Last candidate completed: 1964
- Next candidate sequence: 1965
- Completed candidate recipes: 1054
- Skipped candidates: 5
- Unresolved candidates: 905
- Validation: valid, 0 errors, 2369 warnings
- Images pending in owner handoff queues: 0
- Checkpoint ZIP: `appetee-dataset-v0.4.2-1170-recipes.zip`

## Resume procedure

1. Read `data/AGENTS.md`, `data/START_CODEX_TASK.md`, and `data/DATASET_SPEC.md`.
2. Run `npm run validate` from `data/tools`; do not continue if it fails.
3. Inspect `data/progress.json`, `data/distribution-report.json`, and both lightweight indexes.
4. Begin at candidate 1965 and process `data/recipe_name_candidates.json` strictly in sequence.
5. Write the next recipe as REC-1171 and any new ingredient as ING-0104; never leave a partial record.
6. Research a real matching recipe, resolve ingredients, calculate/classify, and persist candidate metadata. Record duplicates as skipped and unsourceable names as unresolved.
7. Acquire images in order: exact source/product page, then distinct related real-food search for recipes, then AI only as the final fallback; keep unresolved images in both handoff queues.
8. Run image queue audit, build, validation, checkpoint, and create a ZIP named with the actual count.

## Operating targets

- Approximately 200 new valid recipes per checkpoint, or the largest safe partial checkpoint.
- Candidate order controls names; distribution analysis reports drift and never authorizes reordering.
- Candidate plan: approximately 85% Main Meal / 15% other, 30% athlete, 25% Meal Prep, and 10% Discovery.
- Derive Gluten Free and Lactose Free only from ingredient compatibility.
