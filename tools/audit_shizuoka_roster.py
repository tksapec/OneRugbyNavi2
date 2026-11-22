#!/usr/bin/env python3
"""Reconcile Shizuoka's reviewed 2026-27 roster against official club profiles."""

from __future__ import annotations

import json
from pathlib import Path

from normalize_player_history import normalize_player

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Resources" / "Raw" / "league-one-players-2026-27.json"
CHECKED_ON = "2026-11-15"
TEAM_ID = "124"
TEAM_NAME = "静岡ブルーレヴズ"
ROSTER_URL = "https://www.shizuoka-bluerevs.com/member"

# Profile positions are taken from the club's current member cards. Keep the
# club's exact position labels because multi-position strings are shown by the app.
CURRENT_POSITIONS = {
    13695: ["プロップ"], 13696: ["プロップ"], 13697: ["プロップ"], 13698: ["プロップ"],
    13699: ["プロップ"], 13700: ["プロップ"], 13702: ["プロップ"], 13703: ["プロップ"],
    13704: ["プロップ"], 13784: ["プロップ"], 13705: ["フッカー"], 13707: ["フッカー"],
    13708: ["フッカー"], 13701: ["フッカー"], 13709: ["ロック"], 13710: ["ロック"],
    13711: ["ロック"], 13713: ["ロック"], 13714: ["ロック"], 13715: ["ロック/フランカー"],
    13716: ["ロック"], 13717: ["フランカー"], 13718: ["フランカー"], 13719: ["フランカー"],
    13720: ["フランカー"], 13721: ["フランカー"], 13722: ["フランカー"],
    13712: ["フランカー/ロック"], 13706: ["ナンバーエイト"], 13724: ["ナンバーエイト"],
    13725: ["スクラムハーフ"], 13726: ["スクラムハーフ"], 13727: ["スクラムハーフ"],
    13728: ["スクラムハーフ"], 13729: ["スタンドオフ"], 13730: ["スタンドオフ"],
    13731: ["センター/スタンドオフ"], 13732: ["センター"], 13733: ["センター"],
    13734: ["センター/ウィング"], 13735: ["センター/ウィング"],
    13737: ["センター/ウィング"], 13738: ["センター"], 13739: ["ウィング/センター"],
    13740: ["ウィング"], 13741: ["ウィング"], 13742: ["ウィング"],
    13743: ["ウィング/センター"], 13744: ["ウィング"], 13745: ["ウィング"],
    13746: ["ウィング/センター"], 13748: ["フルバック"], 13785: ["フルバック"],
    13747: ["ユーティリティバックス"], 13736: ["ユーティリティバックス"], 13790: ["フルバック"],
}

# These values came from the reviewed official player profile pages.
PROFILE_FACTS = {
    13704: ("2003-07-22", 176, 105, ["流通経済大学柏高校", "流通経済大学"]),
    13784: ("1999-08-19", 178, 115, ["三本木農業高校"]),
    13713: ("2007-06-21", 203, 100, ["HTSミッデルバーグ高校"]),
    13714: ("2003-07-28", 197, 105, ["川西北陵高校", "近畿大学"]),
    13715: ("2000-01-01", 201, 115, []),
    13716: ("1997-08-10", 199, 120, []),
    13724: ("2002-06-29", 188, 100, []),
    13733: ("2001-11-20", 177, 89, ["長崎北陽台高校", "早稲田大学"]),
    13737: ("2004-09-24", 186, 98, []),
    13738: ("1998-04-10", 177, 100, []),
    13746: ("2002-11-22", 187, 100, ["Jeugland高校", "立正大学"]),
    13785: ("2007-02-06", 183, 85, ["St. Peter’s College"]),
    13790: ("1995-05-17", 186, 91, []),
}

PROFILE_URLS = {
    player_id: f"https://www.shizuoka-bluerevs.com/member/{player_id}"
    for player_id in PROFILE_FACTS
}
PROFILE_URLS[13790] = "https://www.shizuoka-bluerevs.com/member/13790"

NEW_PLAYERS = {
        13790: {
        "playerId": "leagueone-124-13790",
        "leagueOnePlayerId": "",
        "currentTeamId": TEAM_ID,
        "currentTeamName": TEAM_NAME,
        "division": "DIV1",
        "nameJa": "ジョック・キャンベル",
        "nameEn": "JOCK CAMPBELL",
        "aliases": [],
        "positions": ["フルバック"],
        "schools": [],
        "teamHistory": [{"teamId": TEAM_ID, "teamName": TEAM_NAME, "type": "club",
                         "fromSeason": "2026-27", "toSeason": ""}],
        "representativeHistory": [],
        "sources": [],
    }
}


def upsert_source(player: dict, publisher: str, url: str, fields: list[str]) -> None:
    sources = player.setdefault("sources", [])
    item = next((source for source in sources if source.get("url") == url), None)
    if item is None:
        item = {"publisher": publisher, "url": url, "checkedOn": CHECKED_ON, "fields": []}
        sources.append(item)
    item["publisher"] = publisher
    item["checkedOn"] = CHECKED_ON
    item["fields"] = list(dict.fromkeys(item.get("fields", []) + fields))


def run() -> tuple[int, int]:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    players = catalog["players"]
    team_players = [player for player in players
                    if player.get("currentTeamId") == TEAM_ID or
                    player.get("playerId") == "leagueone-124-13723"]
    by_suffix = {int(player["playerId"].rsplit("-", 1)[-1]): player
                 for player in team_players if player["playerId"].startswith("leagueone-124-")
                 and player["playerId"].rsplit("-", 1)[-1].isdigit()}

    for player_number, positions in CURRENT_POSITIONS.items():
        player = by_suffix.get(player_number)
        if player is None:
            player = NEW_PLAYERS[player_number]
            players.append(player)
            by_suffix[player_number] = player
        player["positions"] = positions
        roster_fields = ["nameJa", "nameEn", "currentTeamName", "positions"]
        upsert_source(player, "静岡ブルーレヴズ", ROSTER_URL, roster_fields)
        profile = PROFILE_FACTS.get(player_number)
        if profile:
            birth_date, height_cm, weight_kg, schools = profile
            player["birthDate"] = birth_date
            player["heightCm"] = height_cm
            player["weightKg"] = weight_kg
            if schools:
                player["schools"] = schools
            upsert_source(player, "静岡ブルーレヴズ（選手公式プロフィール）",
                          PROFILE_URLS[player_number],
                          ["nameJa", "nameEn", "positions", "birthDate", "heightCm", "weightKg", "schools"])

    smith = next((player for player in team_players if player.get("playerId") == "leagueone-124-13723"), None)
    # The bundled schema only holds current team catalog members. Keep this
    # departed record attached to the last team id for schema consistency and
    # terminate its explicit history; its ended contract is documented below.
    if smith is not None:
        for history in smith["teamHistory"]:
            if history.get("type", "club") == "club" and history.get("teamId") == TEAM_ID:
                history["toSeason"] = "2025-26"
        smith["teamHistory"] = normalize_player(smith)
        upsert_source(smith, "静岡ブルーレヴズ",
                      "https://www.shizuoka-bluerevs.com/news/3229", ["teamHistory"])

    sato = by_suffix[13785]
    sato["positions"] = ["フルバック"]
    sato["birthDate"], sato["heightCm"], sato["weightKg"] = "2007-02-06", 183, 85
    # Current club player card says FB and 2007-02-06; the short-term 2026
    # Rising announcement conflicts, so the older announcement remains separate.
    upsert_source(sato, "静岡ブルーレヴズ（選手公式プロフィール）",
                  "https://www.shizuoka-bluerevs.com/member/13785",
                  ["nameJa", "nameEn", "positions", "birthDate", "heightCm", "weightKg", "schools"])

    campbell = by_suffix[13790]
    campbell["registrationCategories"] = [{"season": "2026-27", "category": "C"}]
    upsert_source(campbell, "静岡ブルーレヴズ",
                  "https://www.shizuoka-bluerevs.com/news/3245",
                  ["nameJa", "nameEn", "currentTeamName", "positions", "birthDate", "heightCm",
                   "weightKg", "registrationCategories", "teamHistory"])

    # Preserve the temporary signing announcements as evidence, but do not
    # assume they describe the later full 2026-27 profile.
    upsert_source(sato, "静岡ブルーレヴズ",
                  "https://www.shizuoka-bluerevs.com/news/3179",
                  ["birthDate", "positions", "heightCm", "weightKg", "schools"])
    tajima = by_suffix[13784]
    upsert_source(tajima, "静岡ブルーレヴズ",
                  "https://www.shizuoka-bluerevs.com/news/3179",
                  ["birthDate", "positions", "heightCm", "weightKg", "schools", "teamHistory"])

    # Drop the unreviewed alternate RugbyDB position. The official current
    # profile is kept as sole current position evidence after deduplication.
    flooks_dupe = next((player for player in players if player.get("playerId") == "rugbydb-124-21084"), None)
    if flooks_dupe:
        flooks_dupe["positions"] = []
        source = next((item for item in flooks_dupe["sources"]
                       if item.get("url") == "https://rugbydb.tokyo/player/21084.html"), None)
        if source:
            source["fields"] = [field for field in source.get("fields", []) if field != "positions"]

    CATALOG.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return len(CURRENT_POSITIONS), len(PROFILE_FACTS)


if __name__ == "__main__":
    roster_count, profile_count = run()
    print(f"Reviewed current roster records: {roster_count}; profile records: {profile_count}")
