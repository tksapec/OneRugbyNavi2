#!/usr/bin/env python3
"""Merge human-reviewed duplicate player records from League One and RugbyDB."""

from __future__ import annotations

import argparse
import difflib
import json
import re
import unicodedata
from pathlib import Path

from normalize_player_history import normalize_player


ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Resources" / "Raw" / "league-one-players-2026-27.json"

# Same-team records were compared by name, position, profile URL, and available
# official player details. Keep this explicit: fuzzy name matches alone can
# incorrectly join teammates with similar names.
REVIEWED_DUPLICATES = {
    "leagueone-123-izi-sword": "rugbydb-123-13482",
    "leagueone-129-joseph_gavigan": "rugbydb-129-38262",
    "leagueone-138-484411": "rugbydb-138-26208",
    "leagueone-143-%e3%82%ab%e3%82%a6%e3%83%aa%e3%83%bb%e3%83%86%e3%82%a3%e3%83%9a%e3%83%8d%e3%82%b0%e3%83%ac%e3%83%bc%e3%82%b9": "rugbydb-143-32449",
    "leagueone-146-andrew-davidson.php": "rugbydb-146-34389",
    "leagueone-149-19": "rugbydb-149-11680",
    "leagueone-149-56": "rugbydb-149-19632",
}


def append_unique(target: list, values: list, key=lambda value: json.dumps(value, sort_keys=True)) -> None:
    found = {key(value) for value in target}
    for value in values:
        marker = key(value)
        if marker not in found:
            target.append(value)
            found.add(marker)


def merge_record(primary: dict, duplicate: dict) -> None:
    for field in ("nameJa", "nameEn"):
        value = duplicate.get(field, "")
        if value and value != primary.get(field):
            primary.setdefault("aliases", []).append(value)
    for field in ("aliases", "positions", "schools", "teamHistory", "representativeHistory"):
        primary.setdefault(field, [])
        values = duplicate.get(field, [])
        if field == "aliases":
            # RugbyDB IDs belong in source URLs, not in searchable alternate
            # names. Keep genuine textual spellings only.
            values = [value for value in values if not (isinstance(value, str) and re.fullmatch(r"\d+", value.strip()))]
            primary[field] = [value for value in primary[field] if not (
                isinstance(value, str) and re.fullmatch(r"\d+", value.strip())
            )]
        append_unique(primary[field], values)

    sources_by_url = {source.get("url"): source for source in primary.setdefault("sources", [])}
    for source in duplicate.get("sources", []):
        existing = sources_by_url.get(source.get("url"))
        if existing is None:
            clone = dict(source)
            primary["sources"].append(clone)
            sources_by_url[clone.get("url")] = clone
        else:
            append_unique(existing.setdefault("fields", []), source.get("fields", []), key=str)
            if source.get("checkedOn", "") > existing.get("checkedOn", ""):
                existing["checkedOn"] = source["checkedOn"]

    for field in ("leagueOnePlayerId", "nameEn", "birthDate", "heightCm", "weightKg"):
        if not primary.get(field) and duplicate.get(field):
            primary[field] = duplicate[field]


def profile_url(player_id: str) -> str:
    return f"https://rugbydb.tokyo/player/{player_id.rsplit('-', 1)[-1]}.html"


def normalized_name(value: str) -> str:
    return "".join(char for char in unicodedata.normalize("NFKC", value or "").upper()
                   if unicodedata.category(char)[0] in ("L", "N"))


def validate_pair(primary: dict, duplicate: dict, duplicate_id: str) -> None:
    if primary.get("currentTeamId") != duplicate.get("currentTeamId"):
        raise ValueError(f"Reviewed pair no longer shares a current team: {primary['playerId']} / {duplicate_id}")
    if primary.get("birthDate") and duplicate.get("birthDate") and primary["birthDate"] != duplicate["birthDate"]:
        raise ValueError(f"Reviewed pair has conflicting birth dates: {primary['playerId']} / {duplicate_id}")
    similarity = difflib.SequenceMatcher(None, normalized_name(primary.get("nameJa", "")),
                                         normalized_name(duplicate.get("nameJa", ""))).ratio()
    if similarity < 0.80:
        raise ValueError(f"Reviewed pair names no longer match: {primary['playerId']} / {duplicate_id}")
    if not any(source.get("url") == profile_url(duplicate_id)
               for player in (primary, duplicate)
               for source in player.get("sources", [])):
        raise ValueError(f"Reviewed RugbyDB profile URL is missing: {duplicate_id}")


def merge_reviewed_pair(players: list[dict], primary_id: str, duplicate_id: str) -> bool:
    by_id = {player["playerId"]: player for player in players}
    primary = by_id.get(primary_id)
    duplicate = by_id.get(duplicate_id)
    if primary is None:
        raise ValueError(f"Reviewed primary player is missing: {primary_id}")
    if duplicate is None:
        if any(source.get("url") == profile_url(duplicate_id) for source in primary.get("sources", [])):
            return False  # The pair was already merged; its source URL proves it.
        raise ValueError(f"Reviewed duplicate is missing without merge evidence: {duplicate_id}")

    validate_pair(primary, duplicate, duplicate_id)
    merge_record(primary, duplicate)
    players.remove(duplicate)
    return True


def deduplicate(catalog: dict) -> int:
    removed = 0
    for primary_id, duplicate_id in REVIEWED_DUPLICATES.items():
        removed += merge_reviewed_pair(catalog["players"], primary_id, duplicate_id)

    for player in catalog["players"]:
        player["teamHistory"] = normalize_player(player)
        player["aliases"] = list(dict.fromkeys(player.get("aliases", [])))
        player["positions"] = list(dict.fromkeys(player.get("positions", [])))
    return removed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="Write the deduplicated catalog.")
    args = parser.parse_args()
    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    removed = deduplicate(catalog)
    print(f"Duplicate player records removed: {removed}")
    print(f"Players remaining: {len(catalog['players'])}")
    if args.apply:
        CATALOG.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("Catalog updated.")
    else:
        print("Preview only. Pass --apply to update the catalog.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
