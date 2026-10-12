#!/usr/bin/env python3
"""Collect RugbyDB League One season records for the bundled player catalog.

The app never scrapes the web. This offline maintenance tool records source URLs
in the catalog and leaves dates unknown when a period cannot be mapped safely.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import html
import json
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from normalize_player_history import normalize_player

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Resources" / "Raw" / "league-one-players-2026-27.json"
# RugbyDB's `team/{id}.html` pages expose the current team name, while a
# player's season section links to that team's stable ID and its historical
# display name. Always use the season-scoped name from the player page. Never
# substitute the current SportsTeam JSON-LD name into past seasons.
PLAYER_URL = re.compile(r"^https://rugbydb\.tokyo/player/(\d+)\.html$")
SEASON_HEADING = re.compile(r"(?is)<h2\b[^>]*>\s*(20\d{2})年シーズン\s*</h2>")
LEAGUE_TEAM = re.compile(
    r"(?is)<span\b[^>]*class=[\"'][^\"']*player-result-league[^\"']*[\"'][^>]*>\s*"
    r"<a\b[^>]*>(.*?)</a>\s*</span>\s*"
    r"<span\b[^>]*class=[\"'][^\"']*player-result-team[^\"']*[\"'][^>]*>\s*"
    r"<a\b[^>]*href=[\"'][^\"']*/team/(\d+)\.html[\"'][^>]*>(.*?)</a>\s*</span>"
)


def fetch(url: str) -> str:
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "OneRugbyNavi2 player catalog research/1.0"},
    )
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=25) as response:
                return response.read().decode("utf-8", errors="replace")
        except urllib.error.HTTPError as error:
            if error.code == 429 and attempt < 2:
                time.sleep(2 ** (attempt + 1))
                continue
            raise
        except (TimeoutError, urllib.error.URLError):
            if attempt == 2:
                raise
            time.sleep(0.5 * (attempt + 1))
    return ""


def season_range(year: int) -> str | None:
    # RugbyDB's 2023-26 League One pages align with the official season ending
    # that calendar year. The inaugural 2022 calendar-year record has no safe
    # YYYY-YY mapping here, so it is represented with unknown dates.
    if year == 2022:
        return None
    start = year - 1
    return f"{start}-{(start + 1) % 100:02d}"


def parse_player_page(source: tuple[str, str]) -> tuple[str, list[dict[str, str]]]:
    player_id, url = source
    time.sleep(0.2)
    try:
        page = fetch(url)
    except Exception as error:  # Keep other players moving after a bad page.
        print(f"page error {player_id}: {error}", file=sys.stderr)
        return player_id, []

    headings = list(SEASON_HEADING.finditer(page))
    records: list[dict[str, str]] = []
    for index, heading in enumerate(headings):
        end = headings[index + 1].start() if index + 1 < len(headings) else len(page)
        section = page[heading.end():end]
        year = int(heading.group(1))
        for league, team_id, team_name in LEAGUE_TEAM.findall(section):
            if html.unescape(re.sub(r"(?is)<[^>]+>", "", league)).strip() != "リーグワン":
                continue
            if year < 2022 or year > 2026:
                continue
            records.append({
                "year": str(year),
                "teamId": team_id,
                "teamName": html.unescape(re.sub(r"(?is)<[^>]+>", "", team_name)).strip(),
            })
    return player_id, records


def parse_team_page(source: tuple[str, str]) -> tuple[str, dict[str, str]]:
    team_id, url = source
    time.sleep(0.1)
    try:
        page = fetch(url)
    except Exception as error:
        print(f"team error {team_id}: {error}", file=sys.stderr)
        return team_id, {}

    scripts = re.findall(r"(?is)<script\b[^>]*type=[\"']application/ld\+json[\"'][^>]*>(.*?)</script>", page)
    for script in scripts:
        try:
            data = json.loads(script)
        except json.JSONDecodeError:
            continue
        graph = data.get("@graph", []) if isinstance(data, dict) else []
        for item in graph:
            if isinstance(item, dict) and item.get("@type") == "SportsTeam":
                return team_id, {
                    "name": str(item.get("name", "")),
                    "alternateName": str(item.get("alternateName", "")),
                }
    return team_id, {}


def load_cache(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def collect(catalog: dict, workers: int) -> tuple[dict[str, list[dict[str, str]]], dict[str, dict[str, str]]]:
    pages: dict[str, str] = {}
    for player in catalog["players"]:
        for source in player.get("sources", []):
            url = source.get("url", "")
            if PLAYER_URL.fullmatch(url):
                pages[player["playerId"]] = url
                break

    sources = sorted(pages.items())
    print(f"Fetching {len(sources)} distinct player profiles from RugbyDB...", flush=True)
    histories: dict[str, list[dict[str, str]]] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        for index, (player_id, records) in enumerate(pool.map(parse_player_page, sources), 1):
            histories[player_id] = records
            if index % 100 == 0 or index == len(sources):
                print(f"Profiles {index}/{len(sources)}", flush=True)

    team_ids = sorted({record["teamId"] for rows in histories.values() for record in rows})
    print(f"Fetching {len(team_ids)} distinct team names...", flush=True)
    team_names: dict[str, dict[str, str]] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=min(workers, 5)) as pool:
        team_sources = ((team_id, f"https://rugbydb.tokyo/team/{team_id}.html") for team_id in team_ids)
        for team_id, record in pool.map(parse_team_page, team_sources):
            team_names[team_id] = record
    return histories, team_names


def apply_records(catalog: dict, histories: dict, team_names: dict, checked_on: str) -> tuple[int, int, int]:
    player_map = {player["playerId"]: player for player in catalog["players"]}
    updated_players = 0
    added_rows = 0
    pages_with_club_rows = 0
    for player_id, source_rows in histories.items():
        player = player_map[player_id]
        by_team: dict[tuple[str, str], set[str]] = {}
        for source in source_rows:
            canonical = source["teamName"]
            team_key = (source["teamId"], canonical)
            by_team.setdefault(team_key, set()).add(source["year"])

        histories_to_add: list[dict[str, str]] = []
        existing = player.setdefault("teamHistory", [])
        for (team_id, team_name), years in by_team.items():
            mapped: set[str] = {season for year in years if (season := season_range(int(year))) is not None}
            unknown_2022 = "2022" in years and "2022-23" not in mapped
            ranges: list[tuple[str, str]] = []
            ordered_years = sorted(int(season[:4]) for season in mapped)
            if ordered_years:
                start = previous = ordered_years[0]
                for year in ordered_years[1:]:
                    if year == previous + 1:
                        previous = year
                        continue
                    ranges.append((f"{start}-{(start + 1) % 100:02d}", f"{previous}-{(previous + 1) % 100:02d}"))
                    start = previous = year
                ranges.append((f"{start}-{(start + 1) % 100:02d}", f"{previous}-{(previous + 1) % 100:02d}"))

            # A season roster is evidence for that season only. Do not use a
            # missing season between records to extend the tenure range.
            candidates = [
                {"teamId": f"rugbydb:team:{team_id}", "teamName": team_name, "type": "club", "fromSeason": start, "toSeason": end}
                for start, end in ranges
            ]
            if unknown_2022:
                candidates.append({"teamId": f"rugbydb:team:{team_id}", "teamName": team_name, "type": "club", "fromSeason": "", "toSeason": ""})

            for candidate in candidates:
                same_team = [row for row in existing if row.get("teamName") == team_name and row.get("type", "club") == "club"]
                if not candidate["fromSeason"]:
                    # An undated 2022-only source record cannot be assigned a
                    # YYYY-YY period. Existing affiliation rows already cover it.
                    if same_team:
                        continue
                else:
                    candidate_start = int(candidate["fromSeason"][:4])
                    candidate_end = int(candidate["toSeason"][:4])
                    covered = False
                    for row in same_team:
                        row_start = row.get("fromSeason", "")
                        row_end = row.get("toSeason", "")
                        if not row_start or not re.fullmatch(r"20\d{2}-\d{2}", row_start):
                            continue
                        start_year = int(row_start[:4])
                        end_year = int(row_end[:4]) if re.fullmatch(r"20\d{2}-\d{2}", row_end or "") else 9999
                        if start_year <= candidate_start and end_year >= candidate_end:
                            covered = True
                            break
                    if covered:
                        continue
                    # Fill an unconfirmed existing period only if RugbyDB gives
                    # one continuous observed span and no unknown 2022 entry.
                    undated = [row for row in same_team if not row.get("fromSeason") and not row.get("toSeason")]
                    dated_candidates = [row for row in candidates if row["fromSeason"]]
                    if len(undated) == 1 and len(dated_candidates) == 1 and not unknown_2022:
                        undated[0]["fromSeason"] = candidate["fromSeason"]
                        undated[0]["toSeason"] = candidate["toSeason"]
                        continue
                histories_to_add.append(candidate)

        if not histories_to_add:
            continue
        existing.extend(histories_to_add)
        sources = player.setdefault("sources", [])
        profile_url = next((source.get("url") for source in sources if PLAYER_URL.fullmatch(source.get("url", ""))), None)
        if profile_url:
            source = next(source for source in sources if source.get("url") == profile_url)
            if "teamHistory" not in source.setdefault("fields", []):
                source["fields"].append("teamHistory")
            source["checkedOn"] = checked_on
        updated_players += 1
        added_rows += len(histories_to_add)
        pages_with_club_rows += 1
    return updated_players, added_rows, pages_with_club_rows


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workers", type=int, default=5)
    parser.add_argument("--cache-file", type=Path, help="Reuse fetched candidate data from a previous collection.")
    parser.add_argument("--write-cache", type=Path, help="Save fetched candidate data for review or later application.")
    parser.add_argument("--apply", action="store_true", help="Write reviewed candidate histories into the catalog.")
    parser.add_argument("--checked-on", default="2026-10-10")
    args = parser.parse_args()

    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    if args.cache_file:
        cache = load_cache(args.cache_file)
        histories, team_names = cache["histories"], cache["teamNames"]
    else:
        histories, team_names = collect(catalog, max(1, min(args.workers, 8)))
    if args.write_cache:
        args.write_cache.parent.mkdir(parents=True, exist_ok=True)
        args.write_cache.write_text(json.dumps({"histories": histories, "teamNames": team_names}, ensure_ascii=False, indent=2), encoding="utf-8")

    total = sum(len(rows) for rows in histories.values())
    print(f"Profiles with League One season rows: {sum(bool(rows) for rows in histories.values())}")
    print(f"Season-level team rows gathered: {total}")
    if not args.apply:
        print("Preview only. Pass --apply after reviewing the candidate cache.")
        return 0

    updated, added, _ = apply_records(catalog, histories, team_names, args.checked_on)
    for player in catalog["players"]:
        player["teamHistory"] = normalize_player(player)
    CATALOG.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Updated players: {updated}; added club history rows: {added}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
