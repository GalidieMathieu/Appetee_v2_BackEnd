# Dataset Execution Plan

## Status

Valid checkpoint at 1170 recipes and 103 canonical ingredients. Dataset growth remains in progress toward >= 2,000 recipes.

## Workstreams

- [x] Build deterministic validation, SQL, index, image-audit, and checkpoint tooling.
- [x] Adopt `recipe_name_candidates.json` as the immutable ordered recipe-name source.
- [x] Persist completed, skipped, unresolved, and next candidate sequences.
- [ ] Continue ordered acquisition at approximately 200 new valid recipes per checkpoint.
- [ ] Stop at the largest fully valid partial checkpoint whenever integrity or system limits make the full target unsafe.
- [ ] Maintain and report student-athlete, Meal Prep, Discovery, meal-category, cuisine, method, nutrition, cost, diet, badge, and source distributions.
- [ ] Reach >= 2,000 validated recipes and create the final versioned snapshot.

Operational details and exact recipe/ingredient/candidate sequences are in `progress.json` and `RESUME.md`.
