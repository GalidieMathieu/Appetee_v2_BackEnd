# Appetee — 2,500 Recipe Names + Non-AI Photo Acquisition

This package contains an optional secondary image tool that searches Wikimedia
Commons for **real, non-AI photographs** and converts accepted photos into
Appetee AVIF assets.

When the owner chooses to acquire images, first attempt the exact selected recipe page with
`npm run images:source` from `data/tools`. Image work is owner-managed outside Codex dataset runs. Use this Wikimedia pipeline manually only when
that source image is unavailable or unsuitable. Do not modify or reorder the
master `data/recipe_name_candidates.json`, and map any accepted image through the
completed recipe's persisted `candidate.sequence` rather than assuming candidate
sequence equals Appetee recipe sequence.

## Important

The ZIP itself does **not** contain 2,500 downloaded web photos.

The ChatGPT execution environment used to prepare this package cannot perform
2,500 independent web-image searches/downloads in one response. The included
tool performs that acquisition on your machine/workspace.

It deliberately does **not** scrape Google Images or arbitrary recipe sites.
The separate exact-source-page tool is repository-owner-authorized for private,
non-production testing and records public reuse as unverified unless evidence
establishes otherwise.

Why:

- a Google result is not a copyright license;
- the first Google result can be copyrighted;
- redistributing thousands of arbitrary web photos in a repository/ZIP is unsafe;
- Appetee should retain image provenance for every asset.

The tool prefers Wikimedia Commons because it exposes structured license and
attribution metadata through the MediaWiki API.

## Candidate distribution

The candidate file contains:

- 2,500 recipe names
- 2,125 Main Meal (85%)
- 375 combined non-main categories (15%)
- 750 student-athlete targets (30%)
- 625 Meal Prep candidates (25%)
- 250 Discovery candidates (10%)

The categories are:

- Main Meal
- Small Meal
- Snack
- Side
- Meal Component
- Dessert
- Drink

## Output

For a successful recipe match:

```text
recipes/
└── 0001_Recipe_Name/
    ├── image-metadata.json
    └── assets/
        ├── main.avif
        └── card.avif
```

`main.avif`

- 1200 x 800
- 3:2
- AVIF
- target roughly 80–150 KB
- maximum 200 KB

`card.avif`

- 480 x 320
- 3:2
- generated from the accepted main image
- target roughly 25–60 KB
- maximum 80 KB

The tool also generates:

```text
image-progress.json
image-manifest.json
missing-images.json
```

## License policy

By default the tool accepts only common licenses that are generally suitable
for redistribution/modification:

- Public domain
- CC0
- CC BY
- CC BY-SA

It rejects unknown licenses and records the reason.

License metadata is preserved in `image-metadata.json`.

For CC BY / CC BY-SA assets, keep the creator/source attribution available in
your application/documentation if you ship those photos.

## Matching policy

The tool searches the actual recipe name and conservative query variants.

It does not intentionally fall back to a generic ingredient photo.

For example:

```text
Chicken Tikka Masala
```

should match a photograph of chicken tikka masala, not generic chicken.

If confidence is too low, the recipe is put in `missing-images.json`. An
unobtrusive photographer credit watermark is acceptable; promotional text,
brand logos, obstructive labels, and generic ingredient images are not.

This is preferable to attaching an incorrect photo.

## Windows setup

From PowerShell:

```powershell
py -m venv .venv
.\\.venv\\Scripts\\Activate.ps1
pip install -r requirements.txt
python tools\\acquire_recipe_images.py --input recipe_name_candidates.json --output recipes
```

Or use:

```powershell
.\\run-images.ps1
```

## Test first

Run only the first 10:

```powershell
python tools\\acquire_recipe_images.py `
  --input recipe_name_candidates.json `
  --output recipes `
  --limit 10
```

Then inspect the image quality and metadata.

## Full run

```powershell
python tools\\acquire_recipe_images.py `
  --input recipe_name_candidates.json `
  --output recipes `
  --limit 2500
```

The job is resumable. Existing successful images are skipped unless
`--overwrite` is specified.

## Retry only missing recipes

```powershell
python tools\\acquire_recipe_images.py `
  --input recipe_name_candidates.json `
  --output recipes `
  --retry-missing
```

## More permissive matching

The default threshold is intentionally conservative.

If too many legitimate photographs are rejected:

```powershell
python tools\\acquire_recipe_images.py `
  --input recipe_name_candidates.json `
  --output recipes `
  --min-score 0.42 `
  --retry-missing
```

Do not lower it aggressively merely to maximize coverage.

## API etiquette

The script:

- identifies itself with a User-Agent;
- sleeps between searches/downloads;
- checkpoints progress;
- retries transient failures;
- does not bypass access controls.

If Wikimedia throttles the job, increase `--delay`.

## Final recommendation

Run this reusable-photo fallback only after exact source-page acquisition.
Visually review every accepted dish match before integrating its assets.

A missing image should not prevent the recipe data itself from being valid.
