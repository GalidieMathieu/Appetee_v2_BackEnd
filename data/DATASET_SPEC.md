# Appetee Development Dataset Specification

**Status:** Approved bulk-generation contract  
**Owner:** Mathieu Galidie  
**Purpose:** Development, realistic UI data, SQL development, EXPLAIN/query-plan analysis, performance testing, and portfolio experimentation.

---

## 1. Dataset Goal

Create at least **2,000 validated recipes** and all canonical ingredients needed by them.

Quality is more important than exactly 2,000. The dataset may grow toward roughly 3,000 recipes when additional entries materially improve diversity or target coverage.

Targets:

- at least 2,000 valid recipes;
- at least 500 `Meal Prep` recipes;
- approximately 30% specifically useful to student athletes;
- approximately 90% familiar/relevant to US users, including common international food;
- approximately 10% `discovery: true` recipes featuring less-common international dishes;
- approximately 85% `Main Meal` and 15% all other meal categories combined, following the predefined candidate plan;
- strong representation of high-protein, budget-friendly, practical meal-prep recipes.

Do not create trivial variants merely to inflate recipe count.

## 1A. Diversity-First Acquisition Strategy

The dataset must be useful at every major intermediate size, not only when it reaches 2,000 recipes.

Treat these milestones as independently usable dataset releases:

```text
100 recipes
250 recipes
500 recipes
1,000 recipes
1,500 recipes
2,000 recipes
```

The first 500 recipes are especially important. They must already behave like a smaller but representative version of the intended final corpus.

Do not generate recipes category-by-category, cuisine-by-cuisine, source-by-source, or protein-by-protein.

Bad pattern:

```text
first 200 = mostly chicken
next 200 = mostly pasta
next 200 = mostly vegetarian
```

Preferred pattern:

```text
every batch mixes cuisines, proteins, meal timing, meal categories, cooking methods,
nutrition profiles, prices, diets, badges, and target use cases
```

Before each new batch:

1. read the current validation/distribution report;
2. identify the most important underrepresented areas;
3. select the next unused names in order from `candidates/recipe-names.json`; do not reorder them to repair gaps;
4. preserve the approved percentages progressively;
5. reject otherwise-valid candidates when they add little useful diversity.

The acquisition objective is not merely to increase recipe count. Diversity reports guide factual classification and document drift, while the predefined ordered candidate list controls which names are attempted next.

### Progressive target preservation

Maintain the approved targets throughout growth rather than fixing them only near the end.

Approximate student-athlete target:

```text
100 recipes  -> ~30
250 recipes  -> ~75
500 recipes  -> ~150
1,000 recipes -> ~300
2,000 recipes -> ~600
```

Approximate Meal Prep target:

```text
100 recipes  -> ~25
250 recipes  -> ~60+
500 recipes  -> ~125+
1,000 recipes -> ~250+
2,000 recipes -> >=500
```

Approximate Discovery target:

```text
100 recipes  -> ~10
250 recipes  -> ~25
500 recipes  -> ~50
1,000 recipes -> ~100
2,000 recipes -> ~200
```

These are directional coverage targets, not reasons to create low-quality filler.

### Diversity dimensions

Actively maintain useful variety across:

- protein source;
- carbohydrate/base;
- cuisine;
- country of origin;
- meal timing (`mealType`);
- meal category (`mealCategory`);
- cooking technique;
- prep time;
- total time;
- difficulty;
- cost per serving;
- calories per serving;
- protein per serving;
- fiber;
- ingredient count;
- diets;
- badges;
- student-athlete suitability;
- Meal Prep suitability;
- Discovery coverage;
- source domains.

### Protein-source diversity

Do not let the high-protein or student-athlete subset become dominated by chicken breast.

Actively include appropriate recipes using sources such as:

- chicken breast;
- chicken thigh;
- turkey;
- lean ground beef;
- other lean beef cuts;
- pork;
- salmon;
- tuna;
- white fish;
- shrimp;
- eggs;
- Greek yogurt;
- cottage cheese;
- beans;
- lentils;
- chickpeas;
- tofu;
- tempeh;
- other practical plant proteins.

No exact equality is required, but no single primary protein should dominate the useful high-protein corpus.

### Carbohydrate/base diversity

Avoid turning the corpus into mostly rice bowls.

Use varied appropriate bases such as:

- jasmine rice;
- basmati rice;
- brown rice;
- potatoes;
- sweet potatoes;
- pasta;
- whole-grain pasta;
- bread;
- tortillas;
- oats;
- quinoa;
- couscous;
- beans;
- lentils;
- noodles;
- vegetables;
- salad bases;
- culturally appropriate starches.

### Meal timing, meal category, and dish-form diversity

These are separate required recipe dimensions:

- `mealType` describes when the recipe is normally served and must be `Breakfast`, `Lunch`, or `Dinner`;
- `mealCategory` describes the recipe's eating role and must be `Main Meal`, `Small Meal`, `Snack`, `Side`, `Meal Component`, `Dessert`, or `Drink`.

Classify each recipe by its actual intended role; do not change a truthful classification merely to hit a checkpoint ratio. `Main Meal` is a substantial complete eating occasion; `Small Meal` is a lighter but complete eating occasion; `Snack` is intended between meals; `Side` accompanies a main dish; `Meal Component` is a base, filling, topping, dip, or other component intended to be combined; `Dessert` is a sweet finishing course; and `Drink` is a beverage.

Across the mature corpus, target approximately 85% `Main Meal` and 15% combined across the other six categories, matching the predefined candidate plan. This is a progressive target, not permission to misclassify a researched dish.

For example, a breakfast smoothie is `Drink` even when it is nutritionally substantial. Do not infer `mealCategory` from `mealType` alone.

Maintain useful coverage across:

- breakfast;
- lunch;
- dinner;
- snacks/small meals;
- full meals;
- bowls;
- soups;
- stews;
- salads;
- sandwiches/wraps;
- pasta;
- rice dishes;
- sheet-pan meals;
- one-pot meals;
- oven meals;
- skillet meals;
- slow-cooked meals;
- cold meal-prep meals.

Do not require identical counts, but the early dataset must not consist almost entirely of dinner entrées.

### Cooking-method diversity

Include varied techniques when appropriate:

- stovetop;
- oven;
- air fryer;
- grill;
- slow cooker;
- no-cook;
- boiled/simmered;
- roasted;
- steamed;
- stir-fried;
- baked;
- braised.

### Preparation-time diversity

Maintain meaningful coverage across approximate buckets:

```text
<=15 minutes
16-30 minutes
31-45 minutes
46-60 minutes
>60 minutes
```

Quick meals should be strongly represented for student use, but not every recipe should qualify as Quick Meal.

### Cost diversity

Maintain useful variation across:

- very inexpensive;
- Budget Friendly;
- moderate-cost;
- higher-cost meals.

Do not artificially alter recipes merely to force a badge.

### Nutrition diversity

Avoid clustering most recipes around one macro profile.

Maintain useful variation in:

- calories;
- protein;
- carbohydrates;
- fat;
- fiber.

The athlete subset should include lighter meals, larger recovery meals, higher-carbohydrate meals, high-protein meals, vegetarian protein options, snacks, and full meals.

### Diet and badge diversity

Do not acquire one diet or badge group at a time.

All allowed diets should appear naturally in the early dataset where appropriate.

Badges remain rule-based. When the current report shows an underrepresented badge, research more legitimate recipes that naturally satisfy it instead of modifying existing recipes merely to obtain the label.

Example:

```text
Current:
90 High Protein
75 Meal Prep
60 Budget Friendly
2 Freezer Friendly
4 High Fiber

Next batches:
prioritize legitimate Freezer Friendly and High Fiber candidates
```

### Cuisine and source diversity

Maintain a broad cuisine mix from the beginning.

The familiar-to-US portion should still draw from multiple cuisines such as American, Mexican, Italian, French, Mediterranean, Greek, Chinese, Japanese, Korean, Thai, Indian, Middle Eastern, Caribbean, and Latin American.

Discovery recipes should progressively broaden underrepresented regions/cuisines.

Do not exhaust one recipe site before moving to another.

Track source-domain counts and intentionally diversify when one source begins to dominate.

### Batch composition

Current default checkpoint target is approximately 200 completed recipes. If validation integrity, execution safety, context, tools, or model allowance cannot support the full target, stop at the largest fully valid partial checkpoint.

Every batch should deliberately mix characteristics based on current gaps.

A typical batch may include:

- multiple cuisines;
- several protein sources;
- meat, fish, vegetarian and/or vegan options;
- different meal timing and meal categories;
- Meal Prep candidates;
- Quick Meal candidates;
- Budget Friendly candidates;
- athlete-oriented recipes;
- approximately 2-4 Discovery candidates when needed to maintain the target;
- different cooking techniques;
- different time and nutrition ranges.

This is a balancing principle, not a rigid quota.

### Milestone reviews

At 100 recipes:

- run full validation;
- inspect diversity;
- confirm the pipeline and data model are sound;
- correct serious issues before scaling.

At 250 recipes:

Review:

- ingredient reuse;
- duplicate rate;
- protein diversity;
- diet coverage;
- badge coverage;
- source concentration;
- image workflow;
- price quality;
- student-athlete percentage;
- Meal Prep percentage;
- Discovery percentage.

At 500 recipes:

This must be a genuinely useful development dataset.

It should support realistic:

- Home development;
- Recipe Discovery;
- compatibility testing;
- filtering;
- cursor pagination;
- lazy loading;
- nutrition/cost filtering;
- badge filtering;
- recommendation experiments;
- SQL EXPLAIN/query-plan analysis.

Before continuing beyond 500:

1. run full validation;
2. regenerate all SQL/indexes;
3. generate distribution reports;
4. create a checkpoint ZIP;
5. update `workflow/RESUME.md`;
6. record specific coverage gaps for recipes 501-1000.

At 1,000 recipes:

Treat the corpus as production-scale for Appetee development purposes and evaluate whether each additional batch materially improves diversity, filter coverage, meal-plan usefulness, cuisine coverage, ingredient relationships, or performance-test realism.

At 1,500 and 2,000 recipes:

Become increasingly strict about repetitive candidates. Prefer recipes that add a new dish, cuisine, technique, ingredient combination, nutrition profile, dietary use case, price profile, badge, meal timing, or meal category.

### Diversity reporting

Extend `generated/reports/validation.json` and/or generated distribution reports with:

- recipe count;
- student-athlete count;
- Meal Prep count;
- Discovery count;
- diet distribution;
- badge distribution;
- country distribution;
- source-domain distribution;
- protein-source distribution;
- meal-type distribution;
- meal-category distribution;
- cooking-method distribution;
- prep-time buckets;
- cost-per-serving buckets;
- calorie-per-serving buckets;
- protein-per-serving buckets.

The validator should warn about obvious imbalance without hard-failing reasonable natural variation.

Examples:

```text
WARNING: Chicken Breast is primary protein in 38% of recipes.
WARNING: Discovery recipes are 3%; target is approximately 10%.
WARNING: Student-athlete recipes are 11%; target is approximately 30%.
WARNING: Breakfast represents only 2% of the corpus.
```

Warnings should directly influence the priorities of the next batch.

### Multi-session / weekly growth

Assume this corpus may be built over multiple Codex usage periods.

A valid growth pattern may be:

```text
usage period 1 -> 300-500 diverse validated recipes
next period    -> 500-1,000 while preserving target distributions
next period    -> 1,000-1,500 while filling gaps
later period   -> 1,500-2,000+ with increasingly strict uniqueness
```

Stopping below 2,000 because resources are exhausted is not failure.

A checkpoint is successful when the current dataset is:

- valid;
- diverse;
- useful;
- fully resumable.

If resources become insufficient, preserve diversity review and validation rather than squeezing in more recipe count.

---

## 2. Repository Placement

The working dataset belongs in the Appetee backend repository under:

```text
/data
```

Reason: it is tightly coupled to MySQL schema, generated seed SQL, backend persistence, and performance/query-plan testing.

Canonical product/feature documentation remains in the separate central Appetee documentation repository.

Do not create a separate dataset repository unless the dataset later acquires an independent lifecycle, access model, or release process.

---

## 3. Required Structure

```text
data/
├── AGENTS.md
├── README.md
├── DATASET_SPEC.md
├── CHANGELOG.md
├── version.json
├── candidates/
│   └── recipe-names.json
├── workflow/
│   ├── START_CODEX_TASK.md
│   ├── RESUME_PROMPT.md
│   ├── PLAN.md
│   ├── RESUME.md
│   └── progress.json
├── ingredients/
│   ├── index.json
│   └── 0001_Ingredient_Name/
│       ├── ingredient.json
│       └── assets/
│           └── image.avif
├── recipes/
│   ├── index.json
│   └── 0001_Recipe_Name/
│       ├── recipe.json
│       └── assets/
│           ├── main.avif
│           └── card.avif
├── fixtures/
│   └── README.md
├── generated/
│   ├── sql/
│   │   ├── 01-schema.sql
│   │   ├── 02-reference.sql
│   │   ├── 03-ingredients.sql
│   │   └── 04-recipes.sql
│   ├── reports/
│   │   ├── validation.json
│   │   ├── distribution.json
│   │   └── images.json
│   ├── manifests/
│   └── snapshots/
├── research/
│   └── image/
│       ├── ingredients.json
│       └── recipes.json
└── tools/
    ├── generation/
    ├── validation/
    ├── image-processing/
    ├── image-acquisition/
    ├── shared/
    ├── database/
    └── deployment/
```

Local AVIF assets exist physically but are ignored by Git.

`fixtures/` contains only small deterministic test data. The full realistic corpus remains canonical under `ingredients/` and `recipes/` and must not be duplicated as fixtures.

Everything under `generated/` is reproducible output. Do not edit generated SQL, reports, manifests, or snapshots manually.

---

## 4. Source-of-Truth Rule

```text
JSON = canonical source data
SQL  = generated artifact
```

Never maintain SQL independently from the JSON.

Ingredient JSON generates ingredient/ingredient-nutrition/price seed data.

Recipe JSON generates recipe, recipe-ingredient, recipe-diet, and recipe-badge seed data.

---

## 5. SQL File Responsibilities

### `generated/sql/01-schema.sql`

Structure only:

- tables;
- primary/foreign keys;
- unique constraints;
- check constraints;
- indexes;
- other database objects.

No normal seed inserts.

The development reset process is a full replacement/recreation, not an incremental update mechanism.

### `generated/sql/02-reference.sql`

Small shared reference data only:

- diets;
- badges.

### `generated/sql/03-ingredients.sql`

Generated from ingredient JSON:

- ingredients;
- ingredient nutrition;
- normalized ingredient pricing;
- other current ingredient persistence fields.

### `generated/sql/04-recipes.sql`

Generated from recipe JSON:

- recipes;
- recipe ingredients;
- recipe diets;
- recipe badges.

Do not seed users, password hashes, user preferences, favorites, or meal plans during this task.

---

## 6. Stable Dataset Identity

MySQL may use AUTO_INCREMENT.

Folder numbers are human-readable dataset sequence IDs, e.g.:

```text
0001_Chicken_Breast
0042_Chicken_Burrito_Bowl
```

Every JSON record also has an immutable seed ID:

```text
ING-0001
REC-0042
```

Recipe JSON references ingredients by ingredient seed ID.

Do not rely on folder sequence == MySQL ID.

---

## 7. Walmart Canonical Market

Use:

**Walmart Supercenter #3789**  
**1959 Wall Ave, Ogden, UT 84401**

Use store-specific product/price context whenever available.

Price data is a **frozen snapshot** for development/testing.

Prefer:

1. generic/mainstream product;
2. commonly sold product;
3. Walmart-sold product rather than third-party marketplace seller;
4. normal/basic version rather than premium specialty version.

Ground beef baseline should be no more than 15% fat.

Current stock availability is not mandatory; the product should genuinely be sold/listed by Walmart.

For an extremely specific non-alcohol ingredient that cannot be obtained reasonably in the US market, reject the recipe unless a sound substitute preserves the dish.

For culturally specific alcohol, an external market-price estimate may be used sparingly when Walmart cannot provide a meaningful price. Record the source.

---

## 8. Ingredient Canonicalization

Prefer exact purchasable ingredients over generic categories.

Examples:

- `Chicken Breast`, not `Chicken`;
- `Chicken Thigh`, not `Chicken`;
- `Jasmine Rice`, `Basmati Rice`, `Brown Rice`, not generic `Rice`;
- `Spaghetti`, `Penne`, `Rigatoni`, not generic `Pasta`;
- `Red Onion`, `Yellow Onion`, `White Onion`, not generic `Onion`.

Use raw/dry purchasable state by default:

- chicken breast: raw;
- rice: dry;
- pasta: dry;
- potatoes: raw.

Before creating an ingredient, search the lightweight ingredient index and existing canonical names. Do not create duplicate aliases for the same product concept.

---

## 9. Ingredient Research Data

Ingredient JSON should retain rich future-use information even if SQL currently stores less.

Include when verifiable:

- seed ID;
- canonical name;
- description;
- solid/liquid measurement type;
- Walmart store/product/name/URL/product ID/brand;
- package price;
- package quantity and package unit;
- seller;
- availability snapshot;
- checked-at timestamp;
- original nutrition label serving;
- normalized nutrition;
- normalized price;
- nutrition source URLs;
- fallback source URLs;
- conversion/density information when useful;
- explicit `dietCompatibility.glutenFree` and `dietCompatibility.lactoseFree` booleans with a classification basis;
- image provenance;
- future API response example.

Never fabricate missing data.

---

## 10. Nutrition Sources

Priority:

1. Walmart nutrition label/product data;
2. USDA FoodData Central or another authoritative/reliable nutrition source;
3. another reliable source only when necessary.

Mandatory dataset values:

- calories;
- protein;
- price.

Preserve useful athlete-oriented data when reliably available:

- carbohydrates;
- fat;
- fiber;
- sugars;
- sodium;
- potassium;
- calcium;
- iron;
- magnesium;
- other trustworthy micronutrients.

Do not invent missing micronutrients.

---

## 11. Normalization

### Solids

- nutrition per 100 g;
- normalized price per 100 g.

### Liquids

- nutrition per 100 ml;
- normalized price per 100 ml.

Keep Walmart package data separate from normalized data.

Keep recipe usage quantity separate from both.

Classify semi-liquids consistently with normal product/package usage; e.g. peanut butter may be gram-based, oils milliliter-based.

Preserve enough conversion/density information to support future recalculation.

---

## 12. Ordered Candidate Selection and Recipe Acquisition

`candidates/recipe-names.json` is the immutable ordered source of future recipe names while unused candidates remain. It is a planning input, not a factual recipe source.

At the start of a run, read `workflow/progress.json.candidateAcquisition.nextCandidateSequence`. Process candidate sequences in ascending order until the requested number of new valid recipes is complete or the stop-before-failure protocol applies. Do not reorder candidates, modify the master candidate file, or invent replacement names.

Before research, compare the candidate with `recipes/index.json`, normalized names, known aliases, ingredient-set similarity when available, and preparation-method similarity. If it is semantically equivalent to an existing recipe, record the sequence and reason as skipped and continue. If no credible substantially matching recipe is available, record it as unresolved and continue without inventing a dish. A source title may differ when it is clearly equivalent.

Every newly acquired recipe must persist `candidate.sequence`, `candidate.seedId`, and `candidate.name`. Candidate progress records completed, skipped, and unresolved sequences and advances only through candidates actually processed in order. Suggested candidate metadata other than the target name and intended category must be independently researched; even the intended category may be corrected when the actual dish requires it.

For each candidate:

1. find a useful, reasonably distinct recipe;
2. record source/provenance;
3. extract factual structure;
4. translate/adapt to English if needed;
5. identify exact canonical ingredients;
6. resolve every ingredient;
7. research/create missing ingredients first;
8. preserve original source measurements;
9. normalize SQL-facing quantities to g/ml;
10. rewrite instructions in concise original Appetee wording;
11. calculate nutrition from canonical ingredient data;
12. calculate price from canonical ingredient data;
13. assign diets, deriving `Gluten Free` and `Lactose Free` from every referenced ingredient's compatibility flags;
14. assign badges;
15. mark the image pending for the documented exact-source, related-real, and last-resort-AI workflow and add it to the generated handoff queue;
16. validate;
17. save JSON;
18. regenerate SQL/indexes at checkpoint.

Do not create a completed recipe before all ingredients resolve.

---

## 13. Sources and Copyright

Use diverse public sources, including established recipe sites, cuisine-specific sites, meal-prep/fitness sites, food publications, public blogs, Instagram, Pinterest, and appropriate international sources.

Instagram/Pinterest may be used for discovery or provenance when the public post/reel itself is the source.

Do not bypass authentication, paywalls, access controls, anti-bot systems, or technical restrictions.

Do not copy copyrighted recipe prose. Facts such as ingredient lists, quantities, temperature, servings, times, and cooking techniques may be used, then instructions must be rewritten in original concise Appetee wording.

Store at least one source URL per recipe.

Actively avoid source-domain concentration.

---

## 14. Language and Cuisine

All Appetee data is stored in English.

When a dish originates elsewhere, prefer a sound source associated with that cuisine when practical, then adapt ingredient terminology/products to the US market without unnecessarily changing the dish.

Every recipe JSON should retain:

- `countryOfOrigin`;
- `discovery` boolean;
- source URL/domain.

Approximately 10% of the corpus should be less-common international discovery recipes.

Do not store duplicate translated copies of the same dish.

---

## 15. Duplicate Recipe Rules

Reject trivial variants.

Two records may coexist when ingredients, technique, dietary construction, or final dish meaningfully differ.

Do not create a new recipe merely because:

- meat quantity changed slightly;
- calorie quantity was reduced slightly;
- it was relabeled `budget`;
- title wording changed.

Use title similarity, ingredient-set similarity, preparation-method similarity, and quantity similarity when deduplicating.

---

## 16. Student-Athlete Focus

Approximately 30% of recipes should be particularly useful to student athletes.

Prioritize a diverse mix of:

- high protein;
- practical carbohydrate sources;
- reasonable calories;
- budget-friendly meals;
- meal-prep-friendly meals;
- common ingredients;
- reasonable preparation time;
- easy reheating;
- different cuisines and protein sources.

Do not make the athlete subset mostly chicken-and-rice variants.

---

## 17. Diets

The only dataset diets are:

- Vegetarian
- Vegan
- Pescatarian
- Keto
- Paleo
- Flexitarian
- Gluten Free
- Lactose Free

Use reasonable mainstream definitions from reputable sources.

`Gluten Free` applies only when every canonical ingredient has `dietCompatibility.glutenFree: true`. Treat oats as gluten free only when the exact selected product is documented as certified gluten free.

`Lactose Free` applies only when every canonical ingredient has `dietCompatibility.lactoseFree: true`. Eggs are not dairy and do not contain lactose. A recipe containing ordinary milk, yogurt, cottage cheese, or another lactose-bearing dairy product is not Lactose Free.

Classify new ingredients from the exact product identity and documented ingredient/allergen information. When evidence is uncertain, use the conservative `false` value and record the reason. Cross-contact is not inferred unless the exact product documentation makes it relevant.

Other allergies are not diets; the application represents them through ingredient restrictions.

Do not over-engineer medical classification precision in this seed corpus.

---

## 18. Badges

Allowed badges:

- High Protein
- Low Calorie
- Low Carb
- High Fiber
- Quick Meal
- Meal Prep
- Freezer Friendly
- Budget Friendly
- Few Ingredients

If canonical Appetee documentation defines a badge rule, use that rule.

Approved fallback rules:

- **High Protein:** >= 30 g protein/serving.
- **Low Calorie:** <= 120 kcal/100 g finished recipe.
- **Low Carb:** <= 30 g carbohydrates/serving.
- **High Fiber:** >= 5.6 g fiber/serving.
- **Quick Meal:** <= 30 minutes mandatory prep + cooking time.
- **Meal Prep:** reasonably prepares ahead, refrigerates about 3 days, and reheats/eats cold acceptably.
- **Freezer Friendly:** freezes and thaws/reheats without unacceptable degradation.
- **Budget Friendly:** <= $4.00 calculated ingredient cost/serving.
- **Few Ingredients:** <= 5 counted ingredients, excluding only water, salt, and black pepper.

At least 500 recipes must have the Meal Prep badge.

---

## 19. Recipe JSON

Preserve more information than current SQL when useful.

Include at least:

- seed ID;
- name;
- concise description (about two UI lines max);
- source URL/domain;
- country of origin;
- discovery boolean;
- student-athlete target boolean;
- prep time;
- cook time;
- total time;
- servings;
- difficulty;
- `mealType` (`Breakfast`, `Lunch`, or `Dinner`);
- `mealCategory` (`Main Meal`, `Small Meal`, `Snack`, `Side`, `Meal Component`, `Dessert`, or `Drink`);
- ingredients;
- source quantities/units/display strings;
- normalized g/ml quantities;
- conversion metadata;
- concise rewritten instructions;
- calculated total nutrition;
- calculated per-serving nutrition;
- source nutrition when available;
- calculated total cost;
- calculated cost/serving;
- diets;
- badges;
- image metadata/provenance;
- representative full future API response.

JSON can contain fields not yet persisted by MySQL.

---

## 20. Recipe Measurement Preservation

Preserve both original and normalized amounts.

Example:

```json
{
  "ingredientSeedId": "ING-0042",
  "name": "Olive Oil",
  "sourceQuantity": 2,
  "sourceUnit": "tbsp",
  "sourceDisplay": "2 tbsp",
  "normalizedQuantity": 30.000,
  "normalizedUnit": "ml"
}
```

Support realistic source units such as cups, tbsp, tsp, oz, lb, cloves, slices, pieces, cans, packages, bunches, and pinches.

Use ingredient-specific conversion information when volume-to-mass conversion is non-trivial.

Do not assume one cup of different ingredients has the same mass.

---

## 21. Nutrition and Cost Calculation

Recipe nutrition is calculated from canonical ingredient nutrition and normalized recipe quantities.

Recipe cost is calculated from normalized canonical ingredient prices and normalized recipe quantities.

Source-site nutrition/cost may be preserved only as comparison metadata.

Use precision of at least 0.001 for intermediate calculations.

Do not repeatedly round intermediate values.

---

## 22. SQL Image Placeholder vs JSON Images

Seed SQL uses the approved known-valid Azure Blob placeholder path currently used by Appetee development data.

The placeholder is a SQL-seed implementation detail only.

JSON must describe the actual local/future image and its provenance.

Do not present the temporary database placeholder as the source image in JSON.

---

## 23. Recipe Image Rules

When a recipe image is available, its directory locally contains:

```text
assets/main.avif
assets/card.avif
```

Main:

- AVIF;
- 1200 x 800;
- 3:2;
- target 80–150 KB;
- maximum 200 KB.

Card:

- generated from main;
- AVIF;
- 480 x 320;
- 3:2;
- target 25–60 KB;
- maximum 80 KB.

No promotional text, brand logos, or obstructive labels. An unobtrusive photographer credit watermark is allowed when it is part of an otherwise suitable real source photograph.

Keep food centered and compatible with `object-fit: cover`.

---

## 24. Ingredient Image Rules

When an ingredient image is available, its directory locally contains:

```text
assets/image.avif
```

Rules:

- AVIF;
- 256 x 256;
- 1:1;
- target 8–25 KB;
- maximum 40 KB;
- centered ingredient;
- consistent simple composition;
- no text, labels, logos, or watermarks.

---

## 25. Image Acquisition and Pending Handoff

When the repository owner performs a dedicated image pass, use this preference order for recipe images:

1. an exact photograph from the selected recipe source page;
2. a distinct related real-food photograph found through web image search;
3. an AI-generated photograph only as the final fallback after both real-image paths have been attempted and documented.

A related real photograph may be approximate, but it must visibly fit the dish theme and be preferable to unnecessary AI generation. A visible photographer-credit watermark is acceptable when it is part of an otherwise suitable photograph. Reject non-food images, placeholders, logos, and promotional graphics. For ingredient images, prefer the exact Walmart product-page image.

For an acquired real image, preserve a precise `imageType` such as `external-real-source-photograph` or `external-real-related-photograph`, `aiGenerated: false`, the source page URL, downloaded/original image URL, discovery provider when applicable, provider, creator/credit when available, factual license/license URL when available, attribution requirement, reuse-verification state, acquisition timestamp, and local paths. Never fabricate missing provenance or license facts, and never label unverified redistribution rights as verified.

For an AI fallback, set `aiGenerated: true`, use an AI-specific `imageType`, and preserve the model/provider, prompt, and generation timestamp. Never describe an AI image as a source photograph.

If no appropriate image is currently available, do not reuse a shared visual fallback. Set `needsGeneratedImage: true`, omit the local AVIF files, and add the record to the pending manifest. The explicit pending state remains valid until the acquisition hierarchy is completed.

At every generation/checkpoint pass, rebuild these ChatGPT handoff queues from pending records:

- `research/image/recipes.json`: `{ "name", "url" }`, where `url` is the recipe source URL;
- `research/image/ingredients.json`: `{ "name", "url" }`, where `url` is the Walmart product URL.

The files are JSON arrays and remain empty when no images are pending.

Dataset-run tooling procedure:

1. new records without an image must start with `needsGeneratedImage: true`, `aiGenerated: false`, and no local AVIF files;
2. bulk dataset-generation runs do not run source search, related search, downloads, or AI generation unless the user explicitly requests a dedicated image task;
3. every record still missing an image remains pending for the repository owner;
4. `npm run images:queue` rebuilds both owner handoff files;
5. `npm run build` also audits existing assets and rebuilds the queues before checkpointing;
6. dedicated image tasks may use `npm run images:source` and `npm run images:related`, followed by visual audit, without changing the bulk-generation rule.

For recipe-image retries, a failed or polluted result set must not immediately trigger AI generation. Record rejected URLs, retry with an independent public image-search provider and/or a simplified dish-form query, and visually audit contact sheets. Compare perceptual hashes as well as exact encoded hashes so differently resized copies of the same photograph are not assigned to multiple recipes. AI remains the last fallback only after exact-source and these independent related-real-photo paths are exhausted.

---

## 26. Git Asset Policy

Available AVIF assets exist locally but are not committed to normal Git history. Explicitly pending records may omit their asset files.

Recommended `.gitignore` rule:

```gitignore
data/**/assets/
```

Commit JSON, SQL, indexes, validators, manifests, reports, specs, and progress files.

---

## 27. Created Dates

Generate realistic, deterministic differing `created_at` values for recipes.

Do not give every recipe the same timestamp.

The dataset should support stable `(created_at, id)` cursor pagination and realistic EXPLAIN/performance testing.

---

## 28. Validation

Critical validation failures include:

### Identity
- duplicate ingredient seed ID;
- duplicate canonical ingredient;
- duplicate recipe seed ID;
- invalid folder/seed relationship.

### References
- recipe references missing ingredient;
- invalid diet;
- invalid badge.

### Ingredient
- missing mandatory product/provenance according to approved fallback policy;
- missing mandatory price;
- missing calories;
- missing protein;
- invalid normalized basis/unit;
- invalid normalized price.

### Recipe
- zero ingredients;
- zero instructions;
- non-positive servings;
- invalid quantities/times;
- invalid or missing `mealType`;
- invalid or missing `mealCategory`;
- unsupported normalized unit;
- nutrition mismatch;
- price mismatch;
- missing source URL.

### Images
- missing asset without an explicit `needsGeneratedImage: true` pending state;
- invalid AVIF;
- wrong dimensions/aspect ratio;
- maximum size exceeded.

Generate `generated/reports/validation.json` with errors, warnings, counts, diet/badge/country/source distributions, missing fields, and pending image work.

Generation of seed SQL must fail on critical validation errors.

---

## 29. Lightweight Index Files

Maintain:

```text
ingredients/index.json
recipes/index.json
```

Ingredient index should include only fields needed to identify/reuse an ingredient.

Recipe index should include lightweight duplicate-detection fields such as normalized name, candidate sequence, ingredient seed IDs, source domain, country, `mealType`, `mealCategory`, and path.

Use these files instead of loading thousands of full JSON records into agent context.

---

## 30. Batch and Checkpoint Protocol

Default checkpoint target: about 200 completed recipes, with the stop-before-failure partial-checkpoint protocol taking precedence over quota.

At every checkpoint:

1. validate;
2. regenerate ingredient SQL;
3. regenerate recipe SQL;
4. regenerate indexes;
5. update `version.json`;
6. update `generated/reports/validation.json`;
7. update `workflow/progress.json`;
8. update `workflow/RESUME.md`;
9. only then start another batch.

If model/tool/context/usage resources appear insufficient, do not start another batch.

Finish a safe checkpoint, create a ZIP snapshot with the actual completed recipe count, record the next sequence, and stop cleanly.

A correct partial corpus is preferable to a broken attempt at 2,000.

---

## 31. Resume Protocol

A fresh session must be able to continue using repository files alone.

Read:

- applicable `AGENTS.md`;
- `DATASET_SPEC.md`;
- `workflow/PLAN.md`;
- `workflow/RESUME.md`;
- `workflow/progress.json`;
- `version.json`;
- `generated/reports/validation.json`;
- lightweight ingredient/recipe indexes.

Validate before continuing.

Resume from `nextRecipeSequence` and `candidateAcquisition.nextCandidateSequence`.

Do not regenerate already valid records unless validation shows they are wrong.

---

## 32. Final Acceptance

Final dataset must have:

- >= 2,000 valid recipes;
- >= 500 Meal Prep recipes;
- ~30% student-athlete target;
- ~10% discovery;
- 0 broken ingredient references;
- 0 duplicate canonical ingredients;
- 0 missing mandatory price/calorie/protein values;
- 0 calculation mismatches.

Quality wins over exact percentage precision.

When complete, regenerate all outputs, mark version status `complete`, and create a final ZIP named with version and actual recipe count.
