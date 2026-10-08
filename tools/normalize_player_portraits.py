#!/usr/bin/env python3
"""Normalize player headshots to 480x640 JPEG using normalized crop boxes.

Usage:
  python tools/normalize_player_portraits.py portraits.csv --input-dir photos --output-dir Resources/Raw/player-portraits

CSV columns: player_id,input,crop_x,crop_y,crop_width,crop_height
Crop coordinates are fractions (0..1) in the source image. Set a crop around head and upper torso.
"""
from __future__ import annotations

import argparse
import csv
from pathlib import Path
from PIL import Image, ImageOps

SIZE = (480, 640)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, default=Path("Resources/Raw/player-portraits"))
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    with args.manifest.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))

    seen: set[str] = set()
    for row in rows:
        player_id = row["player_id"].strip()
        if not player_id or any(ch not in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_" for ch in player_id):
            raise ValueError(f"Unsafe player_id: {player_id!r}")
        if player_id in seen:
            raise ValueError(f"Duplicate player_id: {player_id}")
        seen.add(player_id)

        values = [float(row[key]) for key in ("crop_x", "crop_y", "crop_width", "crop_height")]
        x, y, width, height = values
        if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > 1 or y + height > 1:
            raise ValueError(f"Invalid normalized crop for {player_id}")

        source_path = args.input_dir / row["input"]
        with Image.open(source_path) as original:
            image = ImageOps.exif_transpose(original).convert("RGB")
            left, top = round(x * image.width), round(y * image.height)
            right, bottom = round((x + width) * image.width), round((y + height) * image.height)
            cropped = image.crop((left, top, right, bottom))
            output = ImageOps.fit(cropped, SIZE, method=Image.Resampling.LANCZOS, centering=(0.5, 0.34))
            output.save(args.output_dir / f"{player_id}.jpg", format="JPEG", quality=90, optimize=True)

    print(f"Normalized {len(rows)} portraits to {SIZE[0]}x{SIZE[1]} in {args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
