# Research provenance

Batch 0001 uses timestamped Walmart product listings for the canonical market at Walmart Supercenter #3789, 1959 Wall Ave, Ogden, Utah. Every ingredient JSON retains the exact listing URL, product ID, package price and quantity, seller, availability note, and checked-at timestamp.

Nutrition comes from USDA FoodData Central. The first nine API responses and all curated selections are cached in `cache/usda-batch-0001.json`. Remaining selections were resolved from USDA's public-domain SR Legacy April 2018 JSON archive:

`https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_sr_legacy_food_json_2018-04.zip`

The large source archive and extracted copy are reproducible workflow inputs and are excluded from the checkpoint ZIP; the compact curated cache is included.

Known disclosed approximations:

- The 87%-lean ground turkey product uses USDA's closest 85%-lean generic profile.
- Red lentils use USDA's generic raw lentil profile.
- Berbere uses USDA paprika only as an explicit nutrition proxy because SR Legacy has no generic berbere entry; its product identity and price are exact.
- Liquid USDA mass-basis values are converted with the densities recorded in ingredient JSON.

No source-site recipe prose is redistributed. Instructions are original concise rewrites. Codex dataset runs do not acquire or generate images; missing images remain in `image/recipes.json` and `image/ingredients.json` for the repository owner's separate image workflow.
