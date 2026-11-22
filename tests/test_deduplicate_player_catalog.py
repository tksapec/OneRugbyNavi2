import copy
import json
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

    def test_identical_duplicate_names_do_not_claim_alias_provenance(self):
        primary = {
            "playerId": "leagueone-same", "currentTeamId": "team",
            "nameJa": "同一選手", "nameEn": "Same Player", "aliases": [],
            "positions": [], "schools": [], "teamHistory": [],
            "representativeHistory": [], "sources": [],
        }
        duplicate = {
            "playerId": "rugbydb-99999", "currentTeamId": "team",
            "nameJa": "同一選手", "nameEn": "Same Player", "aliases": [],
            "positions": [], "schools": [], "teamHistory": [],
            "representativeHistory": [],
            "sources": [{"url": "https://rugbydb.tokyo/player/99999.html", "fields": ["nameJa", "nameEn"]}],
        }

        self.assertTrue(deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"]))
        self.assertEqual([], primary["aliases"])
        self.assertEqual([], primary["sources"])

    def test_merge_fails_closed_if_team_or_identity_evidence_changed(self):
        primary, duplicate = self.pair()
        duplicate["currentTeamId"] = "other-team"
        with self.assertRaisesRegex(ValueError, "current team"):
            deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"])

    def test_reviewed_blue_sharks_pairs_are_already_merged_and_idempotent(self):
        catalog_path = Path(__file__).resolve().parents[1] / "Resources" / "Raw" / "league-one-players-2026-27.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        by_id = {player["playerId"]: player for player in catalog["players"]}
        pairs = [
            ("leagueone-137-17300", "rugbydb-137-2526", "日髙 駿", "日高駿", ["LOCK"]),
            ("leagueone-137-17327", "rugbydb-137-4468", "髙橋 広大", "高橋広大", ["FLANKER"]),
            ("leagueone-137-24622", "rugbydb-137-26548", "大竹 慶宣", "大竹慶宜", ["FLANKER"]),
        ]
        for primary_id, duplicate_id, primary_name, duplicate_name, expected_positions in pairs:
            with self.subTest(primary=primary_id):
                primary = copy.deepcopy(by_id[primary_id])
                self.assertNotIn(duplicate_id, by_id)
                self.assertEqual(duplicate_id, deduplicator.REVIEWED_DUPLICATES[primary_id])
                self.assertEqual((primary_name, duplicate_name),
                                 deduplicator.REVIEWED_NAME_VARIANTS[(primary_id, duplicate_id)])

                synthetic_duplicate = {
                    "playerId": duplicate_id,
                    "currentTeamId": primary["currentTeamId"],
                    "nameJa": duplicate_name,
                    "positions": ["TEST POSITION"],
                    "aliases": [],
                    "schools": [],
                    "teamHistory": [],
                    "representativeHistory": [],
                    "sources": [{"url": deduplicator.profile_url(duplicate_id),
                                 "fields": ["nameJa", "positions"]}],
                }
                synthetic_primary = copy.deepcopy(primary)
                players = [synthetic_primary, synthetic_duplicate]
                self.assertTrue(deduplicator.merge_reviewed_pair(players, primary_id, duplicate_id))
                self.assertEqual([synthetic_primary], players)
                self.assertEqual(expected_positions, synthetic_primary["positions"])
                self.assertIn(duplicate_name, synthetic_primary["aliases"])

                # The raw catalog is already deduplicated, and rerunning the reviewed merge stays safe.
                primary = copy.deepcopy(by_id[primary_id])
                self.assertEqual(duplicate_id, deduplicator.REVIEWED_DUPLICATES[primary_id])
                self.assertEqual((primary_name, duplicate_name),
                                 deduplicator.REVIEWED_NAME_VARIANTS[(primary_id, duplicate_id)])
                players = [primary]
                self.assertFalse(deduplicator.merge_reviewed_pair(players, primary_id, duplicate_id))
                self.assertEqual([primary], players)
                self.assertEqual(expected_positions, primary["positions"])
                self.assertIn(duplicate_name, primary["aliases"])
                self.assertTrue(any(source.get("url") == deduplicator.profile_url(duplicate_id)
                                    for source in primary["sources"]))

        primary, duplicate = self.pair()
        duplicate["nameJa"] = "別人の選手"
        with self.assertRaisesRegex(ValueError, "names no longer match"):
            deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"])

    def test_merge_reviewed_urayasu_aliases_and_preserve_official_current_positions(self):
        catalog_path = Path(__file__).resolve().parents[1] / "Resources" / "Raw" / "league-one-players-2026-27.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        by_id = {player["playerId"]: player for player in catalog["players"]}

        vikena = copy.deepcopy(by_id["leagueone-126-1883"])
        vikena_duplicate = {
            "playerId": "rugbydb-126-17020",
            "currentTeamId": "126",
            "nameJa": "ソアネ・ミカエレ ヴィケナ",
            "positions": ["HO"],
            "aliases": [],
            "schools": [],
            "teamHistory": [
                {"teamName": "ハイランダーズ", "type": "club", "toSeason": "2025-26"},
                {"teamName": "浦安D-Rocks", "type": "club", "fromSeason": "2026-27"},
            ],
            "representativeHistory": [],
            "sources": [{"url": "https://rugbydb.tokyo/player/17020.html", "fields": ["nameJa", "positions"]}],
        }
        self.assertEqual("rugbydb-126-17020", deduplicator.REVIEWED_DUPLICATES[vikena["playerId"]])
        self.assertTrue(deduplicator.merge_reviewed_pair([vikena, vikena_duplicate], vikena["playerId"], vikena_duplicate["playerId"]))
        self.assertIn("ソアネ・ミカエレ ヴィケナ", vikena["aliases"])
        self.assertEqual(["HO"], vikena["positions"])
        self.assertTrue(any(row.get("teamName") == "ハイランダーズ" for row in vikena["teamHistory"]))

        larry = copy.deepcopy(by_id["leagueone-126-1837"])
        larry_duplicate = {
            "playerId": "rugbydb-126-13555",
            "currentTeamId": "126",
            "nameJa": "ティポアイールーテル・ラリー",
            "positions": ["No8"],
            "aliases": [],
            "schools": ["倉敷高校", "関東学院大学"],
            "teamHistory": [{"teamName": "浦安D-Rocks", "type": "club", "fromSeason": "2026-27"}],
            "representativeHistory": [],
            "sources": [{"url": "https://rugbydb.tokyo/player/13555.html", "fields": ["nameJa", "positions", "schools", "teamHistory"]}],
        }
        self.assertEqual("rugbydb-126-13555", deduplicator.REVIEWED_DUPLICATES[larry["playerId"]])
        self.assertTrue(deduplicator.merge_reviewed_pair([larry, larry_duplicate], larry["playerId"], larry_duplicate["playerId"]))
        self.assertIn("ティポアイールーテル・ラリー", larry["aliases"])
        self.assertEqual(["FL"], larry["positions"])
        self.assertIn("倉敷高校", larry["schools"])
        self.assertTrue(any(row.get("teamName") == "関東学院大学" for row in larry["teamHistory"]))
        rugbydb_source = next(source for source in larry["sources"]
                              if source.get("url") == "https://rugbydb.tokyo/player/13555.html")
        self.assertNotIn("positions", rugbydb_source["fields"])
        self.assertIn("aliases", rugbydb_source["fields"])

    def test_merge_reviewed_kobe_takao_variant(self):
        catalog_path = Path(__file__).resolve().parents[1] / "Resources" / "Raw" / "league-one-players-2026-27.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        primary = copy.deepcopy(next(player for player in catalog["players"]
                                     if player["playerId"] == "leagueone-127-shigure_takao"))
        duplicate = {
            "playerId": "rugbydb-127-11167",
            "currentTeamId": "127",
            "nameJa": "高尾時流",
            "positions": ["PR"],
            "aliases": [],
            "schools": [],
            "teamHistory": [],
            "representativeHistory": [],
            "sources": [{"url": "https://rugbydb.tokyo/player/11167.html", "fields": ["nameJa", "positions"]}],
        }
        self.assertTrue(deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"]))
        self.assertIn("高尾時流", primary["aliases"])
        self.assertEqual(["プロップ"], primary["positions"])
        self.assertEqual("https://www.kobesteelers.com/member/shigure_takao/",
                         next(source["url"] for source in primary["sources"]
                              if source.get("url", "").endswith("/shigure_takao/")))

    def test_merge_reviewed_shizuoka_flooks_name_order_variant(self):
        catalog_path = Path(__file__).resolve().parents[1] / "Resources" / "Raw" / "league-one-players-2026-27.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        primary = {
            "playerId": "leagueone-124-13746", "currentTeamId": "124",
            "nameJa": "ハインリッヒ・フルックス", "nameEn": "HEINRICH FLOOKS",
            "positions": ["ウィング/センター"], "aliases": [],
            "schools": ["Jeugland高校", "立正大学"], "teamHistory": [],
            "representativeHistory": [], "heightCm": 187, "weightKg": 100,
            "sources": [{"url": "https://www.shizuoka-bluerevs.com/member/13746",
                         "fields": ["nameJa", "positions", "weightKg"]}],
        }
        duplicate = {
            "playerId": "rugbydb-124-21084", "currentTeamId": "124",
            "nameJa": "フルックス・ハインリッヒ", "positions": ["CTB"],
            "aliases": [], "schools": ["立正大学"], "teamHistory": [],
            "representativeHistory": [],
            "sources": [{"url": "https://rugbydb.tokyo/player/21084.html", "fields": ["nameJa", "positions"]}],
        }
        self.assertTrue(deduplicator.merge_reviewed_pair([primary, duplicate], primary["playerId"], duplicate["playerId"]))
        self.assertIn("フルックス・ハインリッヒ", primary["aliases"])
        self.assertEqual(["ウィング/センター"], primary["positions"])
        self.assertEqual(100, primary["weightKg"])

    def test_merge_reviewed_toshiba_roster_variants_and_keep_club_positions(self):
        catalog_path = Path(__file__).resolve().parents[1] / "Resources" / "Raw" / "league-one-players-2026-27.json"
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        by_id = {player["playerId"]: player for player in catalog["players"]}
        pairs = [
            ("leagueone-129-ryusei_koyanagi", "rugbydb-129-28120", "小柳竜晟", "PR", ["プロップ"]),
            ("leagueone-129-yoshitaka_tokunaga", "rugbydb-129-2111", "徳永祥尭", "FL", ["ナンバーエイト"]),
            ("leagueone-129-kohei_takahashi", "rugbydb-129-8801", "高橋昴平", "SH", ["スクラムハーフ"]),
            ("leagueone-129-masaki_hamada", "rugbydb-129-3383", "濱田将暉", "WTB", ["ウイング"]),
        ]
        for primary_id, duplicate_id, duplicate_name, duplicate_position, expected_positions in pairs:
            with self.subTest(primary=primary_id):
                primary = copy.deepcopy(by_id[primary_id])
                duplicate = {
                    "playerId": duplicate_id, "currentTeamId": "129", "nameJa": duplicate_name,
                    "positions": [duplicate_position], "aliases": [], "schools": [],
                    "teamHistory": [], "representativeHistory": [],
                    "sources": [{"url": f"https://rugbydb.tokyo/player/{duplicate_id.rsplit('-', 1)[-1]}.html",
                                 "fields": ["nameJa", "positions"]}],
                }
                self.assertEqual(duplicate_id, deduplicator.REVIEWED_DUPLICATES[primary_id])
                players = [primary, duplicate]
                self.assertTrue(deduplicator.merge_reviewed_pair(players, primary_id, duplicate_id))
                self.assertEqual([primary], players)
                self.assertEqual(expected_positions, primary["positions"])
                self.assertIn(duplicate_name, primary["aliases"])

if __name__ == "__main__":
    unittest.main()
