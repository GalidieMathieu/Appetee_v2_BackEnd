# Appetee Dataset — AGENTS.md

## Mission

Build and maintain the realistic Appetee development dataset under this `data/` directory.

The target is at least 2,000 high-quality validated recipes, with quality and realism taking precedence over hitting an arbitrary count.

## Required Context

Before changing dataset content:

1. Read `DATASET_SPEC.md`.
2. Read `workflow/progress.json`.
3. Read `workflow/RESUME.md`.
4. Read `generated/reports/validation.json`.
5. Use `ingredients/index.json` and `recipes/index.json` for lookup when they exist; do not load the entire dataset into context.

`DATASET_SPEC.md` is the canonical dataset contract. This file only defines permanent working rules.

## Scope

Dataset work may create or change:

- dataset JSON source records;
- generated seed SQL;
- dataset manifests/indexes;
- validators and generators under `tools/`;
- local AVIF assets;
- dataset documentation/checkpoints;
- schema seed copies needed by the dataset contract.

Do not independently implement:

- Recipe Discovery API;
- frontend screens;
- Redis;
- authentication;
- users/favorites;
- meal-plan algorithms;
- Azure deployment.

If a schema change is needed for the approved dataset, document it clearly. Do not opportunistically refactor unrelated backend code.

## Diversity-First Rule

Every intermediate corpus must already be useful and representative.

Before every batch, inspect current distributions and report material drift. Candidate selection itself follows the ordered candidate file below; do not invent, reorder, or cherry-pick names to repair a distribution.

The first 500 recipes must be a diverse standalone development dataset, not merely the first quarter of a future corpus.

Maintain the approved approximate targets progressively:

- ~30% student-athlete-oriented;
- ~25% Meal Prep so the final 2,000 reaches at least 500;
- ~10% Discovery;
- ~90% familiar/relevant to US users;
- ~85% Main Meal and ~15% all other meal categories combined, as planned by the candidate list.

## Ordered Recipe Candidates

`candidates/recipe-names.json` is the immutable ordered planning source for recipe names while unused candidates remain.

For a normal acquisition run:

1. validate the current checkpoint;
2. read `workflow/progress.json.candidateAcquisition.nextCandidateSequence`;
3. process candidates in ascending sequence without reordering;
4. research a real, substantially matching public recipe for each name;
5. skip semantic duplicates and record their sequence/reason;
6. record candidates that cannot yet be sourced as unresolved and continue;
7. persist every completed recipe's candidate sequence and update the cursor at checkpoint.

The candidate name is the acquisition target. Its suggested metadata is not factual authority: verify country, cuisine, meal timing/category, athlete status, Meal Prep, Discovery, diets, badges, method, nutrition, and cost from the researched recipe and canonical rules. Do not modify `candidates/recipe-names.json` during normal generation.

Do not increase recipe count with repetitive low-value variants.

## Source of Truth

- JSON is authoritative.
- SQL is generated from JSON.
- Never fix generated SQL without fixing the JSON source.
- Recipe JSON references ingredients by stable seed identifiers.
- Folder sequence numbers are human dataset identifiers and do not need to equal MySQL AUTO_INCREMENT IDs.

## Research Integrity

Never fabricate:

- recipe source URLs;
- Walmart product URLs/IDs;
- Walmart prices;
- package sizes;
- nutrition values;
- image licenses;
- country provenance.

Use live public sources when researching. Prefer Walmart Supercenter #3789, 1959 Wall Ave, Ogden, UT 84401 for Walmart price/product context.

If required data cannot be verified, use an approved documented fallback or reject the candidate.

Do not bypass authentication, paywalls, access controls, anti-bot controls, or technical restrictions.

## Batch Rule

Work toward approximately 200 recipes per checkpoint unless the current user instruction specifies another size. The mandatory stop-before-failure protocol permits and requires a smaller fully valid partial checkpoint when the full target is unsafe.

A recipe is complete only when:

- every ingredient reference resolves;
- required ingredient data exists;
- source provenance exists;
- measurements normalize correctly;
- nutrition and cost calculations validate;
- diets/badges validate;
- required asset state is valid;
- JSON validates;
- SQL has been regenerated;
- indexes/manifests and checkpoint files are updated.

Every recipe must set both `mealType` (`Breakfast`, `Lunch`, or `Dinner`) and `mealCategory` (`Main Meal`, `Small Meal`, `Snack`, `Side`, `Meal Component`, `Dessert`, or `Drink`). Respect the candidate's intended category unless the researched dish proves it inaccurate; the candidate plan targets approximately 85% Main Meal and 15% combined other roles. A missing image may use the documented explicit pending state.

Every ingredient must explicitly set `dietCompatibility.glutenFree` and `dietCompatibility.lactoseFree` from exact product evidence or a conservative documented classification. Every recipe must derive `Gluten Free` and `Lactose Free` from all referenced ingredients; do not assign either restriction diet from a recipe title or cuisine assumption.

Image acquisition is handled separately by the repository owner during bulk dataset growth. Bulk runs must not search for, download, or generate recipe or ingredient images unless the user explicitly requests a dedicated image task. New records remain explicitly pending with `aiGenerated: false` and no local AVIF paths. Every build/checkpoint must rebuild `research/image/recipes.json` with each pending recipe's name and source URL and `research/image/ingredients.json` with each pending ingredient's name and Walmart product URL. During a dedicated recipe-image pass, prefer exact source photos, then visually audited related real-food searches from independent providers or simplified dish-form queries; reject non-food/promotional results and perceptual duplicates. AI is the final fallback only after those real-image paths are genuinely exhausted. Preserve completed assets and honest provenance without modifying them.

Do not begin another batch if available context, tool allowance, or usage appears insufficient to safely finish it.

## Mandatory Stop-Before-Failure Protocol

When resources are becoming insufficient:

1. Finish the current safe record/batch if possible.
2. Run validation.
3. Regenerate SQL and lightweight indexes.
4. Update `version.json`.
5. Update `generated/reports/validation.json`.
6. Update `workflow/progress.json`.
7. Update `workflow/RESUME.md` with exact next steps.
8. Create a ZIP snapshot named with the actual completed recipe count.
9. Stop cleanly.

A valid partial dataset is always preferred to a broken larger one.

Never knowingly leave a half-written recipe or ingredient as a completed record.

## Context Efficiency

- Use indexes/manifests rather than rereading thousands of JSON files.
- Reuse canonical ingredients instead of researching the same product repeatedly.
- Read only records needed for the current batch.
- Write large artifacts directly to files instead of printing them into chat.
- Keep status updates concise.

## Completion Gate

Final completion requires at minimum:

- 2,000 valid recipes;
- 500 recipes with the Meal Prep badge;
- approximately 30% student-athlete-oriented recipes;
- approximately 10% discovery recipes;
- zero broken ingredient references;
- zero duplicate canonical ingredients;
- zero missing mandatory price/calorie/protein data;
- zero calculation mismatches.

The validator and `DATASET_SPEC.md` define the full acceptance criteria.
