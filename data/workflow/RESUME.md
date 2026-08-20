# Dataset Resume State

Checkpoint v0.6.0 is valid and resumable. The corpus is intentionally partial.

## Current state

- Completed recipes: 1699
- Completed ingredients: 121
- Last completed recipe: REC-1699
- Next recipe sequence: 1700
- Next ingredient sequence: 122
- Last candidate processed: 2500
- Last candidate completed: 2500
- Next candidate sequence: 2501
- Candidate plan exhausted: true
- Completed candidate recipes: 1583
- Skipped candidates: 5
- Unresolved candidates: 912
- Validation: valid, 0 errors, 2791 warnings
- Images pending in owner handoff queues: 0
- Checkpoint ZIP: `generated/snapshots/appetee-dataset-v0.6.0-1699-recipes.zip`

## Resume procedure

1. Read `data/AGENTS.md`, `data/workflow/START_CODEX_TASK.md`, and `data/DATASET_SPEC.md`.
2. Run `npm run validate` from `data/tools`; do not continue if it fails.
3. Inspect `data/workflow/progress.json`, `data/generated/reports/distribution.json`, and both lightweight indexes.
4. Create and review a new immutable ordered candidate plan; the current `data/candidates/recipe-names.json` ends at sequence 2500.
5. Write the next recipe as REC-1700 and any new ingredient as ING-0122; never leave a partial record.
6. Research a real matching recipe, resolve ingredients, calculate/classify, and persist candidate metadata. Record duplicates as skipped and unsourceable names as unresolved.
7. Do not search for, download, or generate images through Codex; keep pending images in both owner handoff queues.
8. Run image queue audit, build, validation, checkpoint, and create a ZIP under `data/generated/snapshots/` named with the actual count.

## Operating targets

- Approximately 50 new valid recipes per checkpoint, or the largest safe partial checkpoint.
- Candidate order controls names; distribution analysis reports drift and never authorizes reordering.
- Candidate plan: approximately 85% Main Meal / 15% other, 30% athlete, 25% Meal Prep, and 10% Discovery.
- Derive Gluten Free and Lactose Free only from ingredient compatibility.
