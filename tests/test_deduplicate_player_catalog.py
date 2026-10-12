import sys
import unittest
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import deduplicate_player_catalog as deduplicator  # noqa: E402


class PlayerCatalogDeduplicatorTests(unittest.TestCase):
    def pair(self):
        profile = "https://rugbydb.tokyo/player/32449.html"
        primary = {
            "playerId": "leagueone-example",
            "currentTeamId": "team",
            "nameJa": "カウリ・ティペネｰグレース",
            "positions": ["FB/フルバック"],
            "aliases": [],
            "schools": ["School A"],
            "teamHistory": [{"teamName": "Old Team", "type": "club", "fromSeason": "2022-23"}],
            "representativeHistory": [{"teamName": "Country A", "seasons": ["2024-25"]}],
            "sources": [{"url": profile, "publisher": "RUGBY DATABASE", "fields": ["teamHistory"]}],
        }
        duplicate = {
            "playerId": "rugbydb-team-32449",
            "currentTeamId": "team",
            "nameJa": "カウリ・ティペネグレース",
            "positions": ["WTB"],
            "aliases": ["カウリ・ティペネグレース", "32449"],
            "schools": ["School B"],
            "teamHistory": [{"teamName": "Current Team", "type": "club", "fromSeason": "2025-26"}],
            "representativeHistory": [{"teamName": "Country B", "seasons": ["2023-24"]}],
            "sources": [{"url": profile, "publisher": "RUGBY DATABASE", "fields": ["positions"]}],
        }
        return primary, duplicate

    def test_merge_keeps_positions_aliases_history_representative_history_and_source_fields(self):
        primary, duplicate = self.pair()
        players = [primary, duplicate]

        self.assertTrue(deduplicator.merge_reviewed_pair(players, primary["playerId"], duplicate["playerId"]))

        self.assertEqual([primary], players)
        self.assertEqual(["FB/フルバック", "WTB"], primary["positions"])
        self.assertNotIn("32449", primary["aliases"])
        self.assertIn("カウリ・ティペネグレース", primary["aliases"])
        self.assertEqual(2, len(primary["teamHistory"]))
        self.assertEqual(2, len(primary["representativeHistory"]))
        self.assertEqual(["teamHistory", "positions"], primary["sources"][0]["fields"])
        self.assertFalse(deduplicator.merge_reviewed_pair(players, primary["playerId"], duplicate["playerId"]))

    def test_merge_fails_closed_if_team_or_identity_evidence_changed(self):
        primary, duplicate = self.pair()
        duplicate["currentTeamId"] = "other-team"
        with self.assertRaisesRegex(ValueError, "current team"):
            deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"])

        primary, duplicate = self.pair()
        duplicate["nameJa"] = "別人の選手"
        with self.assertRaisesRegex(ValueError, "names no longer match"):
            deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"])


if __name__ == "__main__":
    unittest.main()
