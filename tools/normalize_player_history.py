#!/usr/bin/env python3
"""Normalize duplicate player affiliation rows and sort the bundled catalog."""

from __future__ import annotations

import argparse
import json
import re
import unicodedata
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Resources" / "Raw" / "league-one-players-2026-27.json"
SEASON = re.compile(r"^(\d{4})-\d{2}$")
TYPE_ORDER = {"highSchool": 0, "university": 1, "club": 2}


def normalized_name(value: str) -> str:
    return "".join(char for char in unicodedata.normalize("NFKC", value).upper() if char.isalnum())


def season_year(value: str) -> int | None:
    match = SEASON.fullmatch(value or "")
    return int(match.group(1)) if match else None


def make_season(year: int) -> str:
    return f"{year}-{(year + 1) % 100:02d}"


def normalize_player(player: dict) -> list[dict]:
    histories = player.get("teamHistory") or []
    current_name = normalized_name(player.get("currentTeamName", ""))
    current_id = player.get("currentTeamId", "")
    groups: dict[tuple[str, str], list[dict]] = {}
    for history in histories:
        kind = history.get("type", "club")
        groups.setdefault((kind, normalized_name(history.get("teamName", ""))), []).append(history)

    normalized: list[dict] = []
    for (kind, _), records in groups.items():
        dated: list[tuple[dict, int, int, bool]] = []
        undated: list[dict] = []
        for history in records:
            start = season_year(history.get("fromSeason", ""))
            end = season_year(history.get("toSeason", ""))
            has_start = start is not None
            if not has_start and end is None:
                undated.append(history)
                continue
            if start is None:
                start = end
            if end is None:
                end = 9999
            dated.append((history, start, end, has_start))

        dated.sort(key=lambda item: (item[1], item[2]))
        index = 0
        while index < len(dated):
            first, start, end, has_start = dated[index]
            index += 1
            earliest_start = first.get("fromSeason", "") if has_start else ""
            team_id = first.get("teamId", "")
            team_name = first.get("teamName", "")
            while index < len(dated):
                next_history, next_start, next_end, next_has_start = dated[index]
                overlaps = next_start <= end
                adjacent = next_start <= end + 1 and has_start and next_has_start
                if not overlaps and not adjacent:
                    break
                index += 1
                if next_has_start and (not has_start or next_start < (season_year(earliest_start) or 9999)):
                    earliest_start = next_history.get("fromSeason", "")
                has_start = has_start or next_has_start
                end = max(end, next_end)
                if (next_history.get("teamId") == current_id or
                        normalized_name(next_history.get("teamName", "")) == current_name):
                    team_id = current_id
                    team_name = player.get("currentTeamName", team_name)

            if first.get("teamId") == current_id or normalized_name(first.get("teamName", "")) == current_name:
                team_id = current_id
                team_name = player.get("currentTeamName", team_name)
            merged = dict(first)
            merged.update({
                "teamId": team_id,
                "teamName": team_name,
                "fromSeason": earliest_start,
                "toSeason": "" if end == 9999 else make_season(end),
            })
            merged.setdefault("type", kind)
            normalized.append(merged)

        if not dated and undated:
            selected = next((history for history in undated if
                             history.get("teamId") == current_id or
                             normalized_name(history.get("teamName", "")) == current_name), undated[0])
            normalized.append(dict(selected))

    def sort_key(history: dict) -> tuple[int, int, int, str]:
        kind = history.get("type", "club")
        start = season_year(history.get("fromSeason", ""))
        if start is None:
            start = season_year(history.get("toSeason", ""))
        return (TYPE_ORDER.get(kind, 3), start if start is not None else 9999,
                season_year(history.get("fromSeason", "")) or 9999, history.get("teamName", ""))

    return sorted(normalized, key=sort_key)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="Write normalized histories to the catalog.")
    args = parser.parse_args()
    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    changed_players = 0
    removed_rows = 0
    reordered_players = 0
    for player in catalog["players"]:
        before = player.get("teamHistory") or []
        after = normalize_player(player)
        if before != after:
            changed_players += 1
            removed_rows += len(before) - len(after)
            if before != sorted(before, key=lambda row: (
                    TYPE_ORDER.get(row.get("type", "club"), 3),
                    season_year(row.get("fromSeason", "")) or
                    season_year(row.get("toSeason", "")) or 9999,
                    row.get("teamName", ""))):
                reordered_players += 1
            player["teamHistory"] = after
    print(f"Players needing normalization: {changed_players}")
    print(f"Duplicate rows removed: {removed_rows}")
    print(f"Players reordered: {reordered_players}")
    if args.apply:
        CATALOG.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("Catalog updated.")
    else:
        print("Preview only. Pass --apply to update the catalog.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
