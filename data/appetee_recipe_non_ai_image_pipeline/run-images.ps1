$ErrorActionPreference = "Stop"

if (-not (Test-Path ".venv")) {
    py -m venv .venv
}

& .\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt

python tools\acquire_recipe_images.py `
    --input recipe_name_candidates.json `
    --output recipes `
    --limit 2500 `
    --checkpoint-every 10 `
    --min-score 0.50 `
    --delay 0.45
