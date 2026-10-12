#!/usr/bin/env python3
"""Apply reviewed, source-backed corrections to known catalog errors."""

from __future__ import annotations

import json
from pathlib import Path

from normalize_player_history import normalize_player

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Resources" / "Raw" / "league-one-players-2026-27.json"
CHECKED_ON = "2026-10-11"


def source(publisher: str, url: str, fields: list[str]) -> dict:
    return {"publisher": publisher, "url": url, "checkedOn": CHECKED_ON, "fields": fields}


def upsert_source(player: dict, evidence: dict) -> None:
    sources = player.setdefault("sources", [])
    current = next((item for item in sources if item.get("url") == evidence["url"]), None)
    if current is None:
        sources.append(evidence)
    else:
        current["publisher"] = evidence["publisher"]
        current["checkedOn"] = CHECKED_ON
        current["fields"] = list(dict.fromkeys(current.get("fields", []) + evidence["fields"]))


def player(catalog: dict, name: str) -> dict:
    matches = [item for item in catalog["players"] if item.get("nameJa") == name]
    if len(matches) != 1:
        raise ValueError(f"Expected one player named {name}, found {len(matches)}")
    return matches[0]


def replace_history(item: dict, team_id: str, team_name: str, from_season: str, to_season: str,
                    *, type_: str = "club") -> None:
    item["teamHistory"] = [history for history in item.get("teamHistory", []) if not (
        history.get("type", "club") == type_ and (
            history.get("teamId") == team_id or history.get("teamName") == team_name
        )
    )]
    item["teamHistory"].append({
        "teamId": team_id, "teamName": team_name, "type": type_,
        "fromSeason": from_season, "toSeason": to_season,
    })


def main() -> int:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    players = catalog["players"]

    # League One 2025-26 archived profiles identify the correct team and player
    # facts for these examples; RugbyDB season tables independently corroborate.
    targeted = {
        "落 和史": ("leagueone-138-484490", "https://league-one.jp/news/6091",
                    "https://rugbydb.tokyo/player/8207.html"),
        "繁松 哲大": ("leagueone-138-483498", "https://league-one.jp/news/6091",
                      "https://rugbydb.tokyo/player/4161.html"),
        "山崎 昇悟": ("leagueone-138-山崎昇悟", "https://league-one.jp/news/6091",
                      "https://rugbydb.tokyo/player/11726.html"),
        "ジョニー・ファアウリ": ("leagueone-135-johnny-faauli", "https://league-one.jp/news/6099",
                              "https://rugbydb.tokyo/player/1552.html"),
        "大山 祥平": ("leagueone-125-oyama.html", "https://league-one.jp/player/484876",
                       "https://rugbydb.tokyo/player/4052.html"),
    }
    for name, (expected_id, official_url, rdb_url) in targeted.items():
        item = player(catalog, name)
        if item["playerId"] != expected_id:
            raise ValueError(f"Unexpected identity for {name}: {item['playerId']}")
        official_fields = ["teamHistory"] if name in ("落 和史", "繁松 哲大") else ["teamHistory", "positions", "birthDate", "heightCm", "weightKg", "schools", "registrationCategories"]
        upsert_source(item, source("JAPAN RUGBY LEAGUE ONE", official_url, official_fields))
        upsert_source(item, source("RUGBY DATABASE（独立系二次データベース）", rdb_url,
                                   ["teamHistory", "positions"]))
        item["sources"] = [entry for entry in item["sources"] if not (
            name == "落 和史" and entry.get("url") == "https://league-one.jp/player/484490"
        )]

    # Official signing notice explicitly dates Ochi's Kamaishi service to
    # 2023-26; the player page has League One appearances in ending years 2023-26.
    ochi = player(catalog, "落 和史")
    replace_history(ochi, "884", "日本製鉄釜石シーウェイブス", "2023-24", "2025-26")

    # The official 6091 notice explicitly labels the first stint "Red
    # Hurricanes Osaka (2021-2022)" and the next "Urayasu D-Rocks (2022-26)".
    # Keep the names and periods as published; do not backfill a prior brand.
    shigematsu = player(catalog, "繁松 哲大")
    shigematsu["teamHistory"] = [history for history in shigematsu.get("teamHistory", []) if not (
        history.get("type", "club") == "club" and history.get("teamId") in {
            "116", "100", "142", "rugbydb:team:720", "rugbydb:team:1008"
        }
    )]
    shigematsu["teamHistory"].extend([
        {"teamId": "142", "teamName": "レッドハリケーンズ大阪", "type": "club", "fromSeason": "2021-22", "toSeason": "2021-22"},
        {"teamId": "100", "teamName": "浦安D-Rocks", "type": "club", "fromSeason": "2022-23", "toSeason": "2025-26"},
    ])

    # RugbyDB player's League One year headings correspond to season end years.
    # These historical team names/typos are confirmed by official name-change
    # notices and 2025-26 player profiles. Keep the non-League One 2022 calendar
    # year period unknown when the source does not map it safely.
    historical_names = {
        "rugbydb:team:718": ("109", "NECグリーンロケッツ東葛"),
        "rugbydb:team:734": ("105", "三重ホンダヒート"),
        "rugbydb:team:723": ("124", "静岡ブルーレヴズ"),
        "rugbydb:team:729": ("127", "コベルコ神戸スティーラーズ"),
        "rugbydb:team:808": ("145", "中国電力レッドレグリオンズ"),
    }
    # Fix duplicate timelines from the rebrands before 2026-27. The official
    # names in the catalog are historical season-specific names; a RugbyDB
    # current team page is not a historical team-name source.
    # The RDB IDs are stable organization IDs. Use official numeric IDs and
    # official period names for League One history through 2025-26.
    for item in players:
        updated = []
        for history in item.get("teamHistory", []):
            mapping = historical_names.get(history.get("teamId"))
            if not mapping or history.get("type", "club") != "club":
                updated.append(history)
                continue
            official_id, official_name = mapping
            start, end = history.get("fromSeason", ""), history.get("toSeason", "")
            # The 2026 calendar year is the 2025-26 season. The following
            # season's current roster entries are already numeric official IDs.
            if history.get("teamId") == "rugbydb:team:720" and start:
                if start >= "2023-24":
                    official_name = "レッドハリケーンズ大阪"
                else:
                    official_id = "116"
                    official_name = "NTTドコモレッドハリケーンズ大阪"
            history = dict(history, teamId=official_id, teamName=official_name)
            updated.append(history)
        item["teamHistory"] = updated

    # Correct the inconsistent label already assigned to 2025-26 Sayama players.
    # 2026-27 official name change took effect after the 2025-26 season.
    for item in players:
        for history in item.get("teamHistory", []):
            if history.get("teamId") == "144" and history.get("teamName") == "川越狭山セコムラガッツ":
                if not history.get("fromSeason") or history["fromSeason"] <= "2025-26":
                    history["teamId"] = "118"
                    history["teamName"] = "狭山セコムラガッツ"

    # Known source typo for the former NTT Communications club.
    for item in players:
        for history in item.get("teamHistory", []):
            if history.get("teamId") == "rugbydb:team:719":
                history["teamName"] = "NTTコミュニケーションズシャイニングアークス東京ベイ浦安"

    # Fill only directly published values for the three Shuttles additions.
    shuttles = {
        "落 和史": {"positions": ["SO / FB"], "heightCm": 182, "weightKg": 93,
                    "schools": ["石見智翠館高等学校", "大東文化大学"], "categories": [("2026-27", "A")]},
        "繁松 哲大": {"positions": ["FL"], "heightCm": 178, "weightKg": 94,
                      "schools": ["札幌山の手高等学校", "明治大学"], "categories": [("2026-27", "A")]},
        "山崎 昇悟": {"positions": ["PR / HO"], "heightCm": 179, "weightKg": 115,
                      "schools": ["浦和高等学校", "筑波大学", "筑波大学大学院"], "categories": [("2026-27", "A")]},
    }
    for name, values in shuttles.items():
        item = player(catalog, name)
        for field in ("positions", "heightCm", "weightKg", "schools"):
            item[field] = values[field]
        categories = [category for category in item.get("registrationCategories", []) if category.get("season") != "2026-27"]
        categories.extend({"season": season, "category": category} for season, category in values["categories"])
        item["registrationCategories"] = categories
        official_source = next(source for source in item["sources"] if source["url"] == "https://league-one.jp/news/6091")
        official_source["fields"] = list(dict.fromkeys(official_source.get("fields", []) + [
            "positions", "birthDate", "heightCm", "weightKg", "schools", "registrationCategories"
        ]))
        if name == "落 和史":
            item["sources"] = [entry for entry in item["sources"] if entry.get("url") != "https://league-one.jp/player/484490"]

    for name, weight in (("ジョニー・ファアウリ", 100), ("大山 祥平", 115)):
        player(catalog, name)["weightKg"] = weight
    upsert_source(player(catalog, "ジョニー・ファアウリ"), source(
        "JAPAN RUGBY LEAGUE ONE", "https://league-one.jp/player/484675",
        ["teamHistory", "positions", "birthDate", "heightCm", "weightKg", "schools", "registrationCategories"]))
    upsert_source(player(catalog, "大山 祥平"), source(
        "JAPAN RUGBY LEAGUE ONE", "https://league-one.jp/player/484876",
        ["teamHistory", "positions", "birthDate", "heightCm", "weightKg", "schools", "registrationCategories"]))

    for item in players:
        histories = item.get("teamHistory", [])
        # Exclude current-club aliases while merging history first; they should
        # not force earlier separate stints into one range.
        normalized = normalize_player({**item, "teamHistory": [
            history for history in histories
            if not (history.get("type", "club") == "club" and
                    (history.get("teamId") == item.get("currentTeamId") or
                     history.get("teamName") == item.get("currentTeamName")))
        ]})
        by_name = {}
        for history in normalized:
            by_name.setdefault((history.get("type", "club"), history.get("teamName")), []).append(history)
        for history in histories:
            if history.get("type", "club") != "club" or not (
                history.get("teamId") == item.get("currentTeamId") or
                history.get("teamName") == item.get("currentTeamName")
            ):
                continue
            same_stints = by_name.get(("club", history.get("teamName")), [])
            if history.get("fromSeason") and same_stints:
                # Attach an open current-roster row only to the observed stint
                # that actually overlaps its start, preserving earlier resumes.
                start = int(history["fromSeason"][:4])
                candidates = [stint for stint in same_stints if
                              not stint.get("fromSeason") or not stint.get("toSeason") or
                              int(stint["fromSeason"][:4]) <= start <= int(stint["toSeason"][:4])]
                if candidates:
                    candidate = candidates[-1]
                    candidate["toSeason"] = history.get("toSeason", "")
                    continue
            normalized.append(history)
        item["teamHistory"] = normalized

    CATALOG.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Updated catalog with source-backed review of {len(targeted)} player records.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
