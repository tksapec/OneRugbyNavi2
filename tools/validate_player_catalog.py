#!/usr/bin/env python3
"""Validate the bundled player catalog's schema, provenance, and identity invariants."""

from __future__ import annotations

import collections
import json
import re
import sys
import unicodedata
from pathlib import Path

from normalize_player_history import normalize_player

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Resources" / "Raw" / "league-one-players-2026-27.json"
SEASON = re.compile(r"^\d{4}-\d{2}$")


def normalized_name(value: str) -> str:
    return "".join(char for char in unicodedata.normalize("NFKC", value).upper() if char.isalnum())


def current_teams() -> dict[str, tuple[str, str]]:
    source = (ROOT / "SeasonCatalog.cs").read_text(encoding="utf-8")
    pattern = re.compile(
        r'new TeamSeasonInfo\("([^"]+)", "(DIV[123])", LeagueOneTeamId: "(\d+)"'
    )
    return {team_id: (name, division) for name, division, team_id in pattern.findall(source)}


def validate(catalog: dict) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    players = catalog.get("players")
    if catalog.get("schemaVersion") != 1 or catalog.get("season") != "2026-27":
        errors.append("Unexpected schema version or season")
    if not isinstance(players, list) or not players:
        return [*errors, "players must be a non-empty array"], warnings

    expected_teams = current_teams()
    by_team: dict[str, list[dict]] = collections.defaultdict(list)
    player_ids = collections.Counter()
    league_ids = collections.Counter()
    for player in players:
        if not isinstance(player, dict):
            errors.append("players contains a non-object value")
            continue
        name = player.get("nameJa") or player.get("playerId") or "<unknown>"
        player_ids[player.get("playerId", "")] += 1
        official_id = player.get("leagueOnePlayerId")
        if official_id:
            league_ids[official_id] += 1
        team_id = player.get("currentTeamId", "")
        by_team[team_id].append(player)
        expected = expected_teams.get(team_id)
        if expected is None:
            errors.append(f"{name}: unknown currentTeamId {team_id}")
        elif (player.get("currentTeamName"), player.get("division")) != expected:
            errors.append(f"{name}: current team name or division does not match SeasonCatalog")
        if not player.get("playerId") or not player.get("nameJa"):
            errors.append(f"{name}: missing playerId or Japanese name")
        aliases = player.get("aliases") or []
        if any(isinstance(alias, str) and alias.strip().isdigit() for alias in aliases):
            errors.append(f"{name}: internal numeric ID remains in aliases")

        sources = player.get("sources")
        if not isinstance(sources, list) or not sources:
            errors.append(f"{name}: no source records")
            sources = []
        for source in sources:
            if not isinstance(source, dict):
                errors.append(f"{name}: malformed source record")
                continue
            if not source.get("publisher") or not source.get("url", "").startswith("https://"):
                errors.append(f"{name}: source publisher or HTTPS URL is missing")
            if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", source.get("checkedOn", "")):
                errors.append(f"{name}: malformed source checkedOn date")
            if not source.get("fields") or not isinstance(source.get("fields"), list):
                errors.append(f"{name}: source fields must be a non-empty array")

        for field in ("nameJa", "nameEn", "positions", "schools", "birthDate", "heightCm", "weightKg",
                      "nationalities", "registrationCategories", "representativeHistory"):
            if player.get(field) and not any(field in source.get("fields", []) for source in sources if isinstance(source, dict)):
                errors.append(f"{name}: non-empty {field} has no source claiming it")
        birth_date = player.get("birthDate", "")
        if birth_date and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", birth_date):
            errors.append(f"{name}: malformed birthDate")
        if player.get("heightCm") is not None and not 140 <= player["heightCm"] <= 220:
            warnings.append(f"{name}: unusual height {player['heightCm']}cm; verify at source")
        if player.get("weightKg") is not None and not 50 <= player["weightKg"] <= 180:
            warnings.append(f"{name}: unusual weight {player['weightKg']}kg; verify at source")

        history = player.get("teamHistory")
        if not isinstance(history, list):
            errors.append(f"{name}: teamHistory must be an array")
            continue
        for row in history:
            if not isinstance(row, dict):
                errors.append(f"{name}: malformed teamHistory row")
                continue
            if row.get("type", "club") not in ("club", "university", "highSchool"):
                errors.append(f"{name}: invalid teamHistory type")
            start, end = row.get("fromSeason", ""), row.get("toSeason", "")
            if (start and not SEASON.fullmatch(start)) or (end and not SEASON.fullmatch(end)):
                errors.append(f"{name}: malformed teamHistory season {start!r}..{end!r}")
            if start and end and int(start[:4]) > int(end[:4]):
                errors.append(f"{name}: reversed teamHistory period {start}..{end}")
        if normalize_player(player) != history:
            errors.append(f"{name}: teamHistory is not normalized or sorted")

        for category in player.get("registrationCategories") or []:
            if category.get("category") not in ("A", "A-1", "A-2", "B", "C"):
                errors.append(f"{name}: invalid registration category")
            if category.get("season") and not SEASON.fullmatch(category["season"]):
                errors.append(f"{name}: malformed registration category season")

    for player_id, count in player_ids.items():
        if not player_id or count > 1:
            errors.append(f"duplicate or blank playerId: {player_id!r}")
    for player_id, count in league_ids.items():
        if count > 1:
            errors.append(f"duplicate leagueOnePlayerId: {player_id}")
    if set(by_team) != set(expected_teams):
        errors.append("Catalog team set differs from SeasonCatalog")

    duplicate_candidates = 0
    for team_id, members in by_team.items():
        exact_names: dict[str, list[str]] = collections.defaultdict(list)
        bio_facts: dict[tuple, list[str]] = collections.defaultdict(list)
        for player in members:
            key = normalized_name(player.get("nameJa", ""))
            if key:
                exact_names[key].append(player.get("nameJa", ""))
            dob = player.get("birthDate")
            if dob:
                signature = (dob, player.get("heightCm"), player.get("weightKg"), tuple(player.get("positions") or []))
                if sum(value is not None and value != () for value in signature[1:]) >= 2:
                    bio_facts[signature].append(player.get("nameJa", ""))
        for names in exact_names.values():
            if len(names) > 1:
                errors.append(f"team {team_id}: duplicate normalized name {names}")
        for names in bio_facts.values():
            if len(names) > 1:
                duplicate_candidates += 1
                warnings.append(f"team {team_id}: same-bio duplicate candidate {names}; identity needs review")

    if len(expected_teams) != len(by_team):
        errors.append(f"Expected {len(expected_teams)} teams; found {len(by_team)}")
    return errors, warnings


def main() -> int:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8-sig"))
    errors, warnings = validate(catalog)
    players = catalog.get("players", [])
    sources = sum(len(player.get("sources") or []) for player in players if isinstance(player, dict))
    print(f"Players: {len(players)}; teams: {len({player.get('currentTeamId') for player in players if isinstance(player, dict)})}; sources: {sources}")
    print(f"Errors: {len(errors)}; review warnings: {len(warnings)}")
    for message in errors:
        print(f"ERROR: {message}")
    for message in warnings:
        print(f"REVIEW: {message}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
