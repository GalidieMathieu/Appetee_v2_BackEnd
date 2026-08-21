# Appetee Development Dataset Contract

## Status and purpose

This is the stable contract for Appetee's finalized development/mock dataset. The current corpus contains 1,699 recipes and 121 canonical ingredients. It supports local development, API/UI testing, search/filter work, nutrition and cost calculations, and deterministic media deployment.

The old bulk-acquisition, candidate-planning, research-cache, image-staging, and checkpoint processes are retired. Historical decisions remain in `CHANGELOG.md`; they are not active operating instructions.

## Authority

Canonical source:

```text
data/ingredients/*/ingredient.json
data/ingredients/*/assets/image.avif
data/recipes/*/recipe.json
data/recipes/*/assets/main.avif
data/recipes/*/assets/card.avif
```

Reproducible output:

```text
data/ingredients/index.json
data/recipes/index.json
data/generated/reports/
data/generated/sql/01-schema.sql
data/generated/sql/02-reference.sql
data/generated/sql/03-ingredients.sql
data/generated/sql/04-recipes.sql
```

JSON and record-local AVIF files are authoritative. Generated files must never be edited directly.

## Directory contract

```text
data/
├── README.md
├── AGENTS.md
├── DATASET_SPEC.md
├── CHANGELOG.md
├── version.json
├── ingredients/
│   ├── index.json
│   └── ####_Name/
│       ├── ingredient.json
│       └── assets/image.avif
├── recipes/
│   ├── index.json
│   └── ####_Name/
│       ├── recipe.json
│       └── assets/
│           ├── main.avif
│           └── card.avif
├── generated/
│   ├── reports/
│   └── sql/
└── tools/
    ├── database/
    ├── deployment/
    ├── generation/
    ├── shared/
    ├── test/
    └── validation/
```

Do not reintroduce separate candidate, workflow, research, fixture, manifest, snapshot, acquisition, or staging trees without a new approved requirement.

## Stable identities

Ingredient identities use:

```text
ING-####
```

Recipe identities use:

```text
REC-####
```

The numeric part must match the beginning of the record directory name. Stable IDs must not be derived from MySQL auto-increment IDs or display names.

Existing embedded `recipe.candidate` objects are retained as record provenance. They must be internally valid and unique, but no external candidate-planning file is required.

## Ingredient requirements

Every ingredient record must retain:

- stable identity, name, and unique canonical name;
- `solid` or `liquid` measurement type;
- per-100 `g` or `ml` normalized nutrition;
- positive normalized price and package snapshot;
- Walmart product provenance;
- USDA nutrition provenance;
- explicit Gluten Free and Lactose Free compatibility with a classification basis;
- image provenance and deployment approval state;
- one valid 256×256 AVIF image no larger than 40 KiB.

Core calories, protein, carbohydrates, and price values are mandatory. Other supported nutrients may be nullable only where the application schema permits it.

## Recipe requirements

Every recipe record must retain:

- stable identity, unique name, description, and HTTPS source provenance;
- country/origin and development classification metadata;
- preparation, cooking, and total time;
- positive serving count and supported difficulty;
- `Breakfast`, `Lunch`, or `Dinner` meal type;
- a supported meal category;
- ordered complete instructions;
- one or more unique ingredient references;
- normalized ingredient quantity/unit and cooking-yield factor;
- calculated total/per-serving nutrition and cost;
- supported diets and rule-derived badges;
- creation timestamp;
- image provenance and deployment approval state;
- one valid 1200×800 main AVIF no larger than 200 KiB;
- one valid 480×320 card AVIF no larger than 80 KiB.

Recipe ingredient IDs must resolve to canonical ingredients. Normalized units must match the referenced ingredient nutrition basis.

## Supported reference values

Diets:

```text
Vegetarian
Vegan
Pescatarian
Keto
Paleo
Flexitarian
Gluten Free
Lactose Free
```

Badges:

```text
High Protein
Low Calorie
Low Carb
High Fiber
Quick Meal
Meal Prep
Freezer Friendly
Budget Friendly
Few Ingredients
```

Meal categories:

```text
Main Meal
Small Meal
Snack
Side
Meal Component
Dessert
Drink
```

Badge and restriction-diet logic is owned by `tools/validation/validate.mjs` and the shared rule modules. Stored classifications must match recalculation.

## Nutrition and cost calculations

For each recipe ingredient:

```text
factor = normalizedQuantity / 100
nutrient contribution = ingredient nutrient per basis × factor
cost contribution = ingredient normalized price per basis × factor
```

Recipe totals are sums of contributions. Per-serving values divide totals by servings. The validator is the executable calculation contract and uses the documented rounding tolerance.

## Deterministic media mapping

Exact Blob names:

```text
dataset/ingredients/{seedId}/image.avif
dataset/recipes/{seedId}/main.avif
dataset/recipes/{seedId}/card.avif
```

`tools/shared/blob-names.mjs` is the single implementation of this naming rule. Generated SQL must use it.

Dataset media is private/test-use unless its record explicitly sets `productionApproved: true`. A tool must never upload unapproved media into an anonymously public container.

Azure synchronization uses `DefaultAzureCredential` and an existing configured container. Every local asset is preflighted for containment, regular-file status, `.avif` extension, and SHA-256 before writes. Remote state is classified from one `dataset/` prefix listing by `sha256`, `seedid`, `assettype`, and `Content-Type: image/avif`; `datasetversion` is audit-only metadata. Missing and changed assets may be uploaded with at most eight workers, unchanged assets are skipped, remote extras are never deleted, and successful synchronization is followed by read-only verification.

## Generated database contract

Generation always writes exactly these ordered SQL files:

1. `01-schema.sql`
2. `02-reference.sql`
3. `03-ingredients.sql`
4. `04-recipes.sql`

The local reset consumes only this fixed list. The generated schema is for destructive local development bootstrap, not production migration deployment.

## Required commands

Run from `data/tools`:

```powershell
npm install
npm test
npm run validate
npm run build
npm run db:reset:local
npm run db:verify:local
npm run images:sync:azure -- --dry-run
npm run images:sync:azure
npm run images:verify:azure
```

`db:reset:local` already performs validation, generation, fixed-order seed execution, and verification. It accepts only a local host with the exact database name `appetee` and has no bypass.

Azure commands read `AzureStorage__AccountUrl` and `AzureStorage__ContainerName` before falling back to development/base appsettings. They do not create containers, alter access policies, embed credentials, or delete remote data. All current assets are unapproved for public redistribution, so they require a non-anonymous development container.

## Maintenance rule

Canonical records are stable. Change them only for an explicitly requested, verified correction. After an approved change:

1. run tooling tests;
2. validate the complete corpus;
3. regenerate indexes, reports, and SQL;
4. inspect generated changes rather than editing them;
5. reset or verify the local database as appropriate;
6. preserve source and media provenance.

A maintenance change is invalid if it leaves broken references, duplicate identities/names/images, calculation drift, unsupported classifications, missing mandatory data, invalid assets, legacy seed Blob names, or database verification failures.
