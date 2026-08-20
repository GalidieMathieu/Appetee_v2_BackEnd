# Dataset Execution Plan

## Status

Valid checkpoint at 1699 recipes and 121 canonical ingredients. Dataset growth remains in progress toward >= 2,000 recipes. The current immutable candidate plan is exhausted at sequence 2500.

## Workstreams

- [x] Build deterministic validation, SQL, index, image-audit, and checkpoint tooling.
- [x] Adopt `candidates/recipe-names.json` as the immutable ordered recipe-name source.
- [x] Persist completed, skipped, unresolved, and next candidate sequences.
- [x] Process the current immutable candidate plan strictly in order.
- [ ] Add and review a new immutable candidate plan before further recipe generation.
- [ ] Continue ordered acquisition at approximately 50 new valid recipes per checkpoint after an ordered candidate source is available.
- [ ] Stop at the largest fully valid partial checkpoint whenever integrity or system limits make the full target unsafe.
- [ ] Maintain and report student-athlete, Meal Prep, Discovery, meal-category, cuisine, method, nutrition, cost, diet, badge, and source distributions.
- [ ] Reach >= 2,000 validated recipes and create the final versioned snapshot.

Operational details and exact recipe/ingredient/candidate sequences are in `workflow/progress.json` and `workflow/RESUME.md`.
