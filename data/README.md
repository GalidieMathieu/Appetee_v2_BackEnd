# Appetee Development Dataset

This directory contains Appetee's reproducible development dataset. It is coupled to the backend schema, seed generation, query development, UI realism, and performance testing.

## Architecture

```text
data/
├── README.md, DATASET_SPEC.md, AGENTS.md, CHANGELOG.md, version.json
├── candidates/                 # Immutable ordered acquisition inputs
│   └── recipe-names.json
├── workflow/                   # Resumable execution state and operator prompts
├── ingredients/                # Canonical ingredient JSON records, assets, and index
├── recipes/                    # Canonical recipe JSON records, assets, and index
├── fixtures/                   # Small deterministic test fixtures only
├── generated/
│   ├── sql/                    # Ordered database reset/seed outputs
│   ├── reports/                # Validation, distribution, and image audits
│   ├── manifests/              # Image provenance/acquisition manifests
│   └── snapshots/              # Versioned checkpoint ZIPs
├── research/                   # Research caches and owner image handoff queues
└── tools/
    ├── generation/
    ├── validation/
    ├── image-processing/
    ├── image-acquisition/      # Optional acquisition implementations
    └── shared/                 # Shared rules imported by multiple tools
```

## Source of truth

Canonical ingredient and recipe JSON is authoritative. Files under `generated/` are reproducible artifacts and must not be edited manually. Run `npm run build` from `data/tools` to regenerate SQL, indexes, reports, and image queues.

The four SQL files are intentionally ordered:

1. `generated/sql/01-schema.sql`
2. `generated/sql/02-reference.sql`
3. `generated/sql/03-ingredients.sql`
4. `generated/sql/04-recipes.sql`

## Start or resume

Read `AGENTS.md`, `DATASET_SPEC.md`, and `workflow/START_CODEX_TASK.md`. For continuation, use `workflow/RESUME.md`, `workflow/progress.json`, and `workflow/RESUME_PROMPT.md`. Distribution and validation state live in `generated/reports/`.

Recipe `mealType` and `mealCategory` remain separate. The canonical diets include Gluten Free and Lactose Free, derived from explicit ingredient compatibility.

## Images and fixtures

Local record assets exist physically but are ignored by Git. `research/image/ingredients.json` and `research/image/recipes.json` are owner handoff queues, while `generated/reports/images.json` and `generated/manifests/` are reproducible audit/provenance outputs.

`fixtures/` is reserved for small, deterministic automated-test data. The full canonical corpus is not a test fixture and should not be duplicated there.

## Git

Commit canonical JSON, tools, indexes, generated SQL, reports, manifests, specifications, and workflow state. Do not commit record AVIF asset folders or checkpoint ZIPs; see the repository `.gitignore`.
