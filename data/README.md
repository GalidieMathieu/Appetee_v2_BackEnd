# Appetee Development Dataset

This directory contains the reproducible development-data source for Appetee.

## Ownership

The dataset lives in `Appetee_v2_BackEnd` because it is coupled to:

- the MySQL schema;
- generated seed SQL;
- backend data semantics;
- SQL query development;
- EXPLAIN/performance testing.

The separate `GalidieMathieu/Appetee` repository remains the canonical product/feature/engineering documentation hub. Product behavior such as Recipe Discovery should continue to be documented there; this folder owns the operational dataset and generation contract.

## Start

Read:

1. `AGENTS.md`
2. `DATASET_SPEC.md`
3. `START_CODEX_TASK.md`

For continuation after a paused Codex run, use:

- `RESUME.md`
- `progress.json`
- `RESUME_PROMPT.md`

## Core rule

JSON is source of truth. SQL is generated.

Recipe JSON keeps meal timing and eating role separate: `mealType` is `Breakfast`, `Lunch`, or `Dinner`; `mealCategory` is `Main Meal`, `Small Meal`, `Snack`, `Side`, `Meal Component`, `Dessert`, or `Drink`. The ordered candidate plan targets approximately 85% Main Meal and 15% combined other roles, without forcing inaccurate classifications.

The canonical diets are Vegetarian, Vegan, Pescatarian, Keto, Paleo, Flexitarian, Gluten Free, and Lactose Free. The two restriction diets are derived from explicit compatibility flags on every referenced ingredient and enforced by validation.

For new recipes, try the selected source page's real dish photograph first and preserve honest private/test-use provenance. Verified reusable real photographs are the secondary option. AI images and generic ingredient photographs are prohibited. Missing real images may remain explicitly pending in the generated `research/image` queues.

Run `npm run images:queue` from `data/tools` after adding or changing pending records. `npm run build` also synchronizes both files automatically.

## Git

Commit JSON, SQL, tools, indexes, manifests, specs, progress and reports.

Do not commit AVIF asset folders. See the repository `.gitignore` snippet supplied with this package.
