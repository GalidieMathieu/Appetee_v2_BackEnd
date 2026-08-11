#!/usr/bin/env python3
"""
Acquire real, non-AI recipe photos from Wikimedia Commons and convert them to
Appetee AVIF assets.

This tool intentionally avoids Google Images and arbitrary recipe-site scraping.
"""

from __future__ import annotations

import argparse
import html
import io
import json
import math
import re
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import quote

import requests
from PIL import Image, ImageOps

try:
    import pillow_avif  # noqa: F401
except Exception:
    # Modern Pillow builds may already provide AVIF. We test at save time.
    pass


COMMONS_API = "https://commons.wikimedia.org/w/api.php"
USER_AGENT = (
    "AppeteeDatasetImageAcquisition/0.1 "
    "(portfolio-development dataset; Wikimedia Commons reusable images)"
)

ALLOWED_LICENSE_FRAGMENTS = (
    "public domain",
    "cc0",
    "cc by ",
    "cc-by-",
    "cc by-sa",
    "cc-by-sa",
    "creative commons attribution",
)

# Terms that carry little visual identity for image matching.
STOPWORDS = {
    "and", "with", "the", "a", "an", "of", "style", "easy", "quick",
    "meal", "prep", "recipe", "healthy", "high", "protein", "low",
}

# Useful dish-format words. Matching one of these is meaningful.
DISH_TERMS = {
    "bowl", "bowls", "curry", "stew", "soup", "chili", "tacos", "taco",
    "wrap", "wraps", "pasta", "penne", "spaghetti", "noodles", "salad",
    "casserole", "skillet", "hash", "pancakes", "oatmeal", "toast",
    "quesadilla", "smoothie", "shake", "parfait", "pilaf", "pulao",
    "adobo", "shakshuka", "tagine", "fried", "rice", "flatbread",
    "sandwich", "meatballs", "muffins", "pudding",
}

SEARCH_REMOVE_PREFIXES = (
    "sheet pan ", "sheet-pan ", "meal prep ", "quick ", "easy ",
    "roasted ", "crispy ", "herbed ", "garlic ", "lemon ",
    "spicy ", "smoky ", "blackened ",
)


@dataclass
class Candidate:
    title: str
    page_url: str
    image_url: str
    thumb_url: str | None
    width: int
    height: int
    mime: str
    creator: str | None
    credit: str | None
    license_name: str | None
    license_url: str | None
    description: str | None
    score: float
    query: str


def clean_html(value: str | None) -> str | None:
    if not value:
        return None
    text = re.sub(r"<[^>]+>", " ", value)
    text = html.unescape(text)
    return re.sub(r"\s+", " ", text).strip() or None


def ext(meta: dict[str, Any], key: str) -> str | None:
    value = meta.get(key)
    if isinstance(value, dict):
        return value.get("value")
    return None


def tokens(text: str) -> set[str]:
    out = set(re.findall(r"[a-z0-9]+", text.lower()))
    return {t for t in out if len(t) >= 3 and t not in STOPWORDS}


def normalized(text: str) -> str:
    return " ".join(re.findall(r"[a-z0-9]+", text.lower()))


def query_variants(recipe_name: str) -> list[str]:
    variants = [recipe_name.strip()]
    lower = recipe_name.lower().strip()

    for prefix in SEARCH_REMOVE_PREFIXES:
        if lower.startswith(prefix):
            variants.append(recipe_name[len(prefix):].strip())

    # Remove a trailing preparation descriptor conservatively.
    reduced = re.sub(
        r"\b(tray bake|sheet pan|skillet|meal prep)\b",
        "",
        recipe_name,
        flags=re.I,
    )
    reduced = re.sub(r"\s+", " ", reduced).strip(" ,-")
    if reduced and reduced.lower() != recipe_name.lower():
        variants.append(reduced)

    # Deduplicate without destroying original spelling.
    result = []
    seen = set()
    for q in variants:
        key = normalized(q)
        if key and key not in seen:
            seen.add(key)
            result.append(q)

    return result[:3]


def license_allowed(name: str | None) -> bool:
    if not name:
        return False
    n = name.lower()
    if "noncommercial" in n or "no derivatives" in n:
        return False
    return any(x in n for x in ALLOWED_LICENSE_FRAGMENTS)


def relevance_score(recipe_name: str, file_title: str, description: str | None) -> float:
    recipe_tokens = tokens(recipe_name)
    haystack = f"{file_title} {description or ''}"
    file_tokens = tokens(haystack)

    if not recipe_tokens or not file_tokens:
        return 0.0

    overlap = recipe_tokens & file_tokens
    coverage = len(overlap) / len(recipe_tokens)

    # Stronger signal when main dish-format terms overlap.
    recipe_dish = recipe_tokens & DISH_TERMS
    file_dish = file_tokens & DISH_TERMS
    dish_bonus = 0.15 if recipe_dish and (recipe_dish & file_dish) else 0.0

    # Exact phrase or near phrase in title.
    rnorm = normalized(recipe_name)
    tnorm = normalized(file_title.replace("File:", ""))
    phrase_bonus = 0.25 if rnorm and rnorm in tnorm else 0.0

    # Reward main protein/key-noun overlap beyond format word.
    semantic_overlap = overlap - DISH_TERMS
    semantic_bonus = min(0.15, 0.04 * len(semantic_overlap))

    return min(1.0, coverage * 0.65 + dish_bonus + phrase_bonus + semantic_bonus)


def commons_search(session: requests.Session, query: str, limit: int = 15) -> list[Candidate]:
    params = {
        "action": "query",
        "format": "json",
        "formatversion": "2",
        "generator": "search",
        "gsrsearch": query,
        "gsrnamespace": "6",
        "gsrlimit": str(limit),
        "prop": "imageinfo",
        "iiprop": "url|mime|size|extmetadata",
        "iiurlwidth": "1200",
        "origin": "*",
    }

    response = session.get(COMMONS_API, params=params, timeout=30)
    response.raise_for_status()
    data = response.json()

    candidates: list[Candidate] = []

    for page in data.get("query", {}).get("pages", []):
        info_list = page.get("imageinfo") or []
        if not info_list:
            continue

        info = info_list[0]
        mime = str(info.get("mime") or "")
        if not mime.startswith("image/"):
            continue
        if mime in {"image/svg+xml", "image/gif"}:
            continue

        width = int(info.get("width") or 0)
        height = int(info.get("height") or 0)
        if width < 600 or height < 400:
            continue

        meta = info.get("extmetadata") or {}
        license_name = clean_html(ext(meta, "LicenseShortName"))
        if not license_allowed(license_name):
            continue

        description = clean_html(
            ext(meta, "ImageDescription")
            or ext(meta, "ObjectName")
            or ext(meta, "Categories")
        )

        title = str(page.get("title") or "")
        score = relevance_score(query, title, description)

        candidates.append(
            Candidate(
                title=title,
                page_url="https://commons.wikimedia.org/wiki/" + quote(
                    title.replace(" ", "_"), safe=":/_()-,"
                ),
                image_url=str(info.get("url") or ""),
                thumb_url=info.get("thumburl"),
                width=width,
                height=height,
                mime=mime,
                creator=clean_html(ext(meta, "Artist")),
                credit=clean_html(ext(meta, "Credit")),
                license_name=license_name,
                license_url=clean_html(ext(meta, "LicenseUrl")),
                description=description,
                score=score,
                query=query,
            )
        )

    return candidates


def choose_candidate(
    session: requests.Session,
    recipe_name: str,
    min_score: float,
    delay: float,
) -> Candidate | None:
    all_candidates: list[Candidate] = []

    for query in query_variants(recipe_name):
        try:
            all_candidates.extend(commons_search(session, query))
        except requests.RequestException as exc:
            print(f"  search failed for {query!r}: {exc}", file=sys.stderr)
        time.sleep(delay)

    if not all_candidates:
        return None

    # Same Commons file can appear for multiple query variants.
    by_url: dict[str, Candidate] = {}
    for item in all_candidates:
        existing = by_url.get(item.image_url)
        if existing is None or item.score > existing.score:
            by_url[item.image_url] = item

    best = max(by_url.values(), key=lambda x: x.score)
    return best if best.score >= min_score else None


def download_image(session: requests.Session, candidate: Candidate) -> Image.Image:
    url = candidate.thumb_url or candidate.image_url
    response = session.get(url, timeout=60)
    response.raise_for_status()

    image = Image.open(io.BytesIO(response.content))
    image.load()

    if image.mode not in ("RGB", "RGBA"):
        image = image.convert("RGB")
    elif image.mode == "RGBA":
        # Flatten transparency to white.
        background = Image.new("RGB", image.size, "white")
        background.paste(image, mask=image.getchannel("A"))
        image = background

    return image


def crop_to_3_2(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return ImageOps.fit(
        image,
        size,
        method=Image.Resampling.LANCZOS,
        centering=(0.5, 0.5),
    )


def save_avif_target(
    image: Image.Image,
    path: Path,
    max_bytes: int,
    target_min: int,
    target_max: int,
) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)

    best: tuple[bytes, int] | None = None
    qualities = [62, 56, 50, 46, 42, 38, 34, 30, 26, 22]

    for quality in qualities:
        buffer = io.BytesIO()
        image.save(buffer, format="AVIF", quality=quality, speed=6)
        payload = buffer.getvalue()
        size = len(payload)

        if size <= max_bytes:
            best = (payload, quality)
            if target_min <= size <= target_max:
                break

    if best is None:
        # Last-resort aggressive compression.
        buffer = io.BytesIO()
        image.save(buffer, format="AVIF", quality=18, speed=6)
        best = (buffer.getvalue(), 18)

    payload, quality = best
    path.write_bytes(payload)
    return quality


def slugify(name: str) -> str:
    slug = re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_")
    return slug[:120] or "Recipe"


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    tmp.replace(path)


def load_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8"))


def process_recipe(
    session: requests.Session,
    recipe: dict[str, Any],
    output_root: Path,
    min_score: float,
    delay: float,
    overwrite: bool,
) -> dict[str, Any]:
    seq = int(recipe.get("sequence") or 0)
    name = str(recipe["name"])
    folder = output_root / f"{seq:04d}_{slugify(name)}"
    assets = folder / "assets"
    main_path = assets / "main.avif"
    card_path = assets / "card.avif"
    metadata_path = folder / "image-metadata.json"

    if not overwrite and main_path.exists() and card_path.exists() and metadata_path.exists():
        existing = load_json(metadata_path, {})
        return {
            "sequence": seq,
            "name": name,
            "status": "already-complete",
            "folder": str(folder),
            "metadata": existing,
        }

    candidate = choose_candidate(session, name, min_score=min_score, delay=delay)

    if candidate is None:
        return {
            "sequence": seq,
            "name": name,
            "status": "missing",
            "reason": "No sufficiently relevant reusable Wikimedia Commons photograph found.",
            "queries": query_variants(name),
        }

    try:
        image = download_image(session, candidate)
    except Exception as exc:
        return {
            "sequence": seq,
            "name": name,
            "status": "download-error",
            "reason": str(exc),
            "sourcePage": candidate.page_url,
        }

    main = crop_to_3_2(image, (1200, 800))
    card = crop_to_3_2(image, (480, 320))

    try:
        q_main = save_avif_target(
            main,
            main_path,
            max_bytes=200 * 1024,
            target_min=80 * 1024,
            target_max=150 * 1024,
        )
        q_card = save_avif_target(
            card,
            card_path,
            max_bytes=80 * 1024,
            target_min=25 * 1024,
            target_max=60 * 1024,
        )
    except Exception as exc:
        return {
            "sequence": seq,
            "name": name,
            "status": "conversion-error",
            "reason": str(exc),
            "sourcePage": candidate.page_url,
        }

    metadata = {
        "recipeName": name,
        "type": "external-reusable-photo",
        "aiGenerated": False,
        "match": {
            "query": candidate.query,
            "score": round(candidate.score, 4),
            "commonsTitle": candidate.title,
            "description": candidate.description,
        },
        "source": {
            "provider": "Wikimedia Commons",
            "pageUrl": candidate.page_url,
            "originalImageUrl": candidate.image_url,
            "creator": candidate.creator,
            "credit": candidate.credit,
            "license": candidate.license_name,
            "licenseUrl": candidate.license_url,
            "attributionRequired": (
                candidate.license_name is not None
                and "cc0" not in candidate.license_name.lower()
                and "public domain" not in candidate.license_name.lower()
            ),
            "licenseMetadataVerifiedVia": "Wikimedia Commons imageinfo/extmetadata API",
        },
        "original": {
            "width": candidate.width,
            "height": candidate.height,
            "mime": candidate.mime,
        },
        "assets": {
            "main": {
                "path": "assets/main.avif",
                "width": 1200,
                "height": 800,
                "bytes": main_path.stat().st_size,
                "quality": q_main,
            },
            "card": {
                "path": "assets/card.avif",
                "width": 480,
                "height": 320,
                "bytes": card_path.stat().st_size,
                "quality": q_card,
                "generatedFromMainSource": True,
            },
        },
    }

    write_json(metadata_path, metadata)

    return {
        "sequence": seq,
        "name": name,
        "status": "complete",
        "folder": str(folder),
        "sourcePage": candidate.page_url,
        "license": candidate.license_name,
        "matchScore": round(candidate.score, 4),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--limit", type=int, default=None)
    parser.add_argument("--start", type=int, default=1)
    parser.add_argument("--min-score", type=float, default=0.50)
    parser.add_argument("--delay", type=float, default=0.45)
    parser.add_argument("--checkpoint-every", type=int, default=10)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--retry-missing", action="store_true")
    args = parser.parse_args()

    recipes = json.loads(args.input.read_text(encoding="utf-8"))
    if not isinstance(recipes, list):
        raise SystemExit("Input JSON must be an array.")

    args.output.mkdir(parents=True, exist_ok=True)
    progress_path = args.output.parent / "image-progress.json"
    manifest_path = args.output.parent / "image-manifest.json"
    missing_path = args.output.parent / "missing-images.json"

    old_manifest = load_json(manifest_path, [])
    manifest_by_seq = {
        int(item["sequence"]): item
        for item in old_manifest
        if isinstance(item, dict) and "sequence" in item
    }

    if args.retry_missing:
        allowed_seqs = {
            int(item["sequence"])
            for item in load_json(missing_path, [])
            if isinstance(item, dict) and "sequence" in item
        }
        recipes = [r for r in recipes if int(r.get("sequence") or 0) in allowed_seqs]

    recipes = [r for r in recipes if int(r.get("sequence") or 0) >= args.start]
    if args.limit is not None:
        recipes = recipes[: args.limit]

    session = requests.Session()
    session.headers.update({"User-Agent": USER_AGENT})

    processed = 0
    for recipe in recipes:
        seq = int(recipe.get("sequence") or 0)
        name = str(recipe.get("name") or "")
        print(f"[{seq:04d}] {name}")

        result = process_recipe(
            session=session,
            recipe=recipe,
            output_root=args.output,
            min_score=args.min_score,
            delay=args.delay,
            overwrite=args.overwrite,
        )
        manifest_by_seq[seq] = result
        processed += 1

        print(
            f"  -> {result['status']}"
            + (
                f" score={result.get('matchScore')}"
                if result.get("matchScore") is not None
                else ""
            )
        )

        if processed % args.checkpoint_every == 0:
            manifest = [manifest_by_seq[k] for k in sorted(manifest_by_seq)]
            write_json(manifest_path, manifest)
            missing = [
                x for x in manifest
                if x.get("status") not in {"complete", "already-complete"}
            ]
            write_json(missing_path, missing)
            write_json(
                progress_path,
                {
                    "processedThisRun": processed,
                    "lastSequence": seq,
                    "complete": sum(
                        1 for x in manifest
                        if x.get("status") in {"complete", "already-complete"}
                    ),
                    "missingOrFailed": len(missing),
                    "minScore": args.min_score,
                },
            )

        time.sleep(args.delay)

    manifest = [manifest_by_seq[k] for k in sorted(manifest_by_seq)]
    missing = [
        x for x in manifest
        if x.get("status") not in {"complete", "already-complete"}
    ]
    write_json(manifest_path, manifest)
    write_json(missing_path, missing)

    complete_count = sum(
        1 for x in manifest
        if x.get("status") in {"complete", "already-complete"}
    )
    write_json(
        progress_path,
        {
            "processedThisRun": processed,
            "lastSequence": (
                int(recipes[-1].get("sequence") or 0) if recipes else None
            ),
            "complete": complete_count,
            "missingOrFailed": len(missing),
            "minScore": args.min_score,
        },
    )

    print()
    print(f"Complete images: {complete_count}")
    print(f"Missing/failed:   {len(missing)}")
    print(f"Manifest:         {manifest_path}")
    print(f"Missing list:     {missing_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
