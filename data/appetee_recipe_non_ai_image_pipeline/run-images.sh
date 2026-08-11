#!/usr/bin/env bash
set -euo pipefail

python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
pip install -r requirements.txt

python tools/acquire_recipe_images.py \
  --input recipe_name_candidates.json \
  --output recipes \
  --limit 2500 \
  --checkpoint-every 10 \
  --min-score 0.50 \
  --delay 0.45
