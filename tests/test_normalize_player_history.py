import sys
import unittest
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from normalize_player_history import normalize_player  # noqa: E402


class PlayerHistoryNormalizerTests(unittest.TestCase):
    def test_merges_adjacent_ended_record_into_open_current_stint(self):
        player = {
            "currentTeamId": "team",
            "currentTeamName": "Current Club",
            "teamHistory": [
                {"teamId": "team", "teamName": "Current Club", "type": "club", "fromSeason": "2024-25", "toSeason": "2024-25"},
                {"teamId": "team", "teamName": "Current Club", "type": "club", "fromSeason": "2025-26", "toSeason": ""},
            ],
        }

        history = normalize_player(player)

        self.assertEqual(1, len(history))
        self.assertEqual(("2024-25", ""), (history[0]["fromSeason"], history[0]["toSeason"]))

    def test_does_not_bridge_a_gap_or_a_documented_return(self):
        player = {
            "currentTeamId": "current",
            "currentTeamName": "Current Club",
            "teamHistory": [
                {"teamId": "old-current", "teamName": "Current Club", "type": "club", "fromSeason": "2020-21", "toSeason": "2020-21"},
                {"teamId": "other", "teamName": "Other Club", "type": "club", "fromSeason": "2021-22", "toSeason": "2022-23"},
                {"teamId": "current", "teamName": "Current Club", "type": "club", "fromSeason": "2024-25", "toSeason": ""},
            ],
        }

        history = normalize_player(player)

        current_stints = [row for row in history if row["teamName"] == "Current Club"]
        self.assertEqual(2, len(current_stints))
        self.assertEqual(("2020-21", "2020-21"), (current_stints[0]["fromSeason"], current_stints[0]["toSeason"]))
        self.assertEqual(("2024-25", ""), (current_stints[1]["fromSeason"], current_stints[1]["toSeason"]))


if __name__ == "__main__":
    unittest.main()
