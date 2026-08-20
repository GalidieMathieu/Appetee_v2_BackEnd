# Start Codex Task — Appetee Bulk Dataset

Run this from the **Appetee_v2_BackEnd** repository, preferably on the `development` branch/worktree where the dataset work belongs.

## Primary Goal

Build the Appetee development dataset described by `data/DATASET_SPEC.md` using a **diversity-first incremental strategy**.

Do not optimize for recipe count alone.

The first 100, 250, and especially 500 recipes must already form a useful and diverse miniature version of the intended final 2,000+ recipe corpus.

If the run stops at 300, 400, or 500 recipes because of Codex usage/context limits, that partial dataset must still be useful for real Appetee development and testing.

## Before doing work

2. Read `data/AGENTS.md`.
3. Read `data/DATASET_SPEC.md`.
4. Read `data/workflow/PLAN.md`.
5. Read `data/workflow/progress.json`.
6. Read `data/workflow/RESUME.md`.
7. Read `data/generated/reports/validation.json`.
8. Read `data/ingredients/index.json` and `data/recipes/index.json` if they exist.
9. Inspect the current backend schema/init SQL only as needed to map the approved dataset contract.
10. Do not modify unrelated backend application code.

## Build the reproducible pipeline first

Establish and validate the tooling needed for:

- JSON schemas/source records;
- canonical ingredient lookup/reuse;
- lightweight recipe and ingredient indexes;
- duplicate detection;
- dataset validation;
- deterministic JSON -> SQL generation;
- nutrition calculation;
- cost calculation;
- measurement normalization;
- AVIF validation/resizing/card derivation for assets already available, plus explicit pending-image handling;
- distribution reporting;
- checkpoint/resume state.

Validate the pipeline with a small real batch before scaling.

## Diversity-first acquisition

Before EVERY new recipe batch:

1. inspect the current validation/distribution report;
2. inspect `workflow/progress.json`;
3. identify the most important coverage gaps;
4. define the batch priorities from those gaps;
5. begin at `workflow/progress.json.candidateAcquisition.nextCandidateSequence` and process `data/candidates/recipe-names.json` strictly in order;
6. research a real, substantially matching recipe for each name;
7. record semantic duplicates as skipped and unsourceable candidates as unresolved, then continue;
8. complete approximately 50 new validated recipes, or the largest safe partial checkpoint.

Do not generate recipes in blocks like:

```text
200 chicken recipes
then 200 pasta recipes
then vegetarian recipes
```

Every batch should mix characteristics based on current needs.

Actively diversify:

- cuisine;
- country;
- protein source;
- carbohydrate/base;
- meal timing (`mealType`);
- meal category (`mealCategory`: `Main Meal`, `Small Meal`, `Snack`, `Side`, `Meal Component`, `Dessert`, or `Drink`), with the candidate plan targeting about 85% Main Meal and 15% combined other roles;
- cooking technique;
- prep time;
- cost;
- calorie range;
- protein range;
- fiber;
- difficulty;
- ingredient count;
- diets;
- badges;
- student-athlete use cases;
- Meal Prep suitability;
- Discovery coverage;
- source domains.

The ordered candidate file controls names. Use diversity reporting to validate realized coverage and document drift; do not reorder or invent candidate names.

For every new ingredient, record explicit gluten-free and lactose-free compatibility from exact product evidence or a conservative documented classification. Derive recipe `Gluten Free` and `Lactose Free` values only when every referenced ingredient qualifies.

## Progressive target preservation

Maintain the approved targets from the beginning:

```text
~30% student-athlete oriented
~25% Meal Prep progressively, reaching >=500 at 2,000
~10% Discovery
~90% familiar/relevant to US users
```

Approximate milestone expectations:

```text
100:
~30 athlete
~25 Meal Prep
~10 Discovery

250:
~75 athlete
~60+ Meal Prep
~25 Discovery

500:
~150 athlete
~125+ Meal Prep
~50 Discovery

1,000:
~300 athlete
~250+ Meal Prep
~100 Discovery

2,000:
~600 athlete
>=500 Meal Prep
~200 Discovery
```

These are directional quality targets, not reasons to create filler.

## First 500 are a real release

The first 500 recipes must already support realistic:

- Home page development;
- Recipe Discovery;
- diet/ingredient compatibility testing;
- recipe search;
- filters;
- cursor pagination;
- lazy loading;
- nutrition filters;
- cost filters;
- badge filters;
- recommendation experiments;
- SQL EXPLAIN/query-plan testing.

Do not assume diversity can simply be repaired after recipe 500.

At recipe 500:

1. stop briefly;
2. run full validation;
3. regenerate SQL/indexes;
4. generate full distribution reports;
5. create a 500-recipe checkpoint ZIP;
6. update `workflow/RESUME.md`;
7. explicitly record the biggest diversity gaps to target in recipes 501-1000.

## Milestone review behavior

Review at:

```text
100
250
500
1,000
1,500
2,000
```

At each milestone:

- inspect overrepresented dimensions;
- inspect underrepresented dimensions;
- update next-batch priorities;
- keep existing target percentages roughly intact.

As the corpus grows, become increasingly strict about duplicate/repetitive recipes.

## Research integrity

Use live public sources according to `DATASET_SPEC.md`.

Never fabricate:

- recipe source URLs;
- Walmart product data;
- prices;
- nutrition;
- package sizes;
- product IDs;
- image licenses;
- country provenance.

Reuse canonical ingredients aggressively so Walmart/nutrition research is not repeated unnecessarily.

Do not bypass authentication, paywalls, access controls, anti-bot systems, or technical restrictions.

Do not spend bulk-generation resources searching for, downloading, or generating images unless the user explicitly requests a dedicated image task. Keep new images explicitly pending with `aiGenerated: false`, preserve existing assets, and run `npm run images:queue` (or `npm run build`) so `research/image/recipes.json` and `research/image/ingredients.json` remain complete owner handoff lists.

## Checkpoint behavior

Default batch size:

```text
~50 completed recipes
```

After EVERY completed batch:

1. validate;
2. regenerate ingredient SQL;
3. regenerate recipe SQL;
4. regenerate lightweight indexes;
5. regenerate/update distribution reports;
6. update `version.json`;
7. update `generated/reports/validation.json`;
8. update `workflow/progress.json`;
9. update `workflow/RESUME.md`;
10. only then begin another batch.

Do not ask for routine confirmation between batches.

## Mandatory resource-limit behavior

If context, usage, tool allowance, model allowance, or available resources appear insufficient to safely finish another complete batch:

**STOP BEFORE FAILURE.**

Do not start another batch.

Before stopping:

1. finish the current safe record/batch if possible;
2. run validation;
3. regenerate current SQL/indexes/reports;
4. update `version.json`;
5. update `generated/reports/validation.json`;
6. update `workflow/progress.json`;
7. update `workflow/RESUME.md`;
8. record exact diversity gaps and recommended priorities for the next session;
9. create a ZIP snapshot containing the currently valid dataset with the actual completed recipe count in the filename;
10. report:
   - completed recipe count;
   - completed ingredient count;
   - validation status;
   - next recipe sequence;
   - major diversity gaps;
   - ZIP path;
   - `workflow/RESUME.md` path;
11. stop cleanly.

A valid and diverse 437-recipe dataset is better than a broken attempt at 500.

Never sacrifice research integrity, diversity review, or validation merely to increase recipe count.

## Continue automatically

Continue toward at least 2,000 high-quality recipes while resources permit.

The dataset may be built over multiple Codex usage periods.

Each period should expand the existing corpus while preserving target percentages and filling measured diversity gaps.

Do not restart between sessions.
