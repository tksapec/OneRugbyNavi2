using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class PlayerCatalogTests
{
    [Fact]
    public void Bundled_catalog_has_sourced_players_for_catalogued_teams()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "league-one-players-2026-27.json"));

        Assert.True(PlayerCatalogParser.TryParse(json, out var catalog, out var errors), string.Join(Environment.NewLine, errors));
        Assert.Equal("2026-27", catalog.Season);
        Assert.True(catalog.Players.Count >= 1000);

        var playersByTeam = catalog.Players.GroupBy(player => player.CurrentTeamId).ToArray();
        Assert.Equal(27, playersByTeam.Length);
        Assert.Equal(SeasonCatalog.Teams2026.Count, playersByTeam.Length);
        Assert.Equal(catalog.Players.Count, catalog.Players.Select(player => player.PlayerId).Distinct().Count());
        var duplicateCandidates = PlayerCatalogDuplicateDetector.FindCandidates(catalog.Players);
        Assert.True(duplicateCandidates.Count == 0, string.Join(Environment.NewLine, duplicateCandidates.Select(candidate =>
            $"{candidate.First.PlayerId} {candidate.First.NameJa} / {candidate.Second.PlayerId} {candidate.Second.NameJa}: {string.Join(",", candidate.Reasons)}")));
        var officialIds = catalog.Players.Where(player => !string.IsNullOrWhiteSpace(player.LeagueOnePlayerId))
            .Select(player => player.LeagueOnePlayerId);
        Assert.Equal(officialIds.Count(), officialIds.Distinct(StringComparer.Ordinal).Count());

        foreach (var teamPlayers in playersByTeam)
        {
            var team = Assert.Single(SeasonCatalog.Teams2026, item => item.LeagueOneTeamId == teamPlayers.Key);
            Assert.All(teamPlayers, player =>
            {
                Assert.Equal(team.TeamName, player.CurrentTeamName);
                Assert.Equal(team.DivisionCode, player.Division);
                Assert.NotEmpty(player.Sources);
            });
        }
    }

    [Fact]
    public void Reviewed_name_variants_resolve_to_one_correct_player_and_bad_identity_names_are_not_aliases()
    {
        var catalog = LoadBundledCatalog();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["パトリク・ヴァカタ"] = "leagueone-125-patrick.html",
            ["高本幹也"] = "leagueone-125-takamoto.html",
            ["尾崎泰雅"] = "leagueone-125-ozaki_t.html",
            ["ジャームズ・ロウ"] = "leagueone-125-james.html",
            ["落和史"] = "leagueone-138-484490",
            ["繁松哲大"] = "leagueone-138-483498",
            ["山崎昇悟"] = "leagueone-138-山崎昇悟"
        };

        foreach (var (query, playerId) in expected)
            Assert.Equal(playerId, Assert.Single(PlayerSearch.Search(catalog.Players, query)).PlayerId);

        Assert.Empty(PlayerSearch.Search(catalog.Players, "越智 一真"));
        Assert.Empty(PlayerSearch.Search(catalog.Players, "重松 鉄平"));
        Assert.Empty(PlayerSearch.Search(catalog.Players, "山崎 翔吾"));

        foreach (var officialName in new[] { "落 和史", "繁松 哲大", "山崎 昇悟" })
        {
            var player = Assert.Single(catalog.Players, item => item.NameJa == officialName);
            Assert.Contains(player.Sources, source => source.Url == "https://league-one.jp/news/6091");
        }

        var dalton = Assert.Single(catalog.Players, item => item.NameEn == "Matthew Dalton");
        Assert.Contains("37805", dalton.Aliases);
        Assert.Contains(dalton.Sources, source => source.Url == "https://rugbydb.tokyo/player/37805.html");
        Assert.DoesNotContain(catalog.Players, item => item.PlayerId == "rugbydb-127-37805");

        var takahashi = Assert.Single(catalog.Players, item => item.LeagueOnePlayerId == "484434");
        Assert.Contains("高橋信之", takahashi.Aliases);
        Assert.Contains("4334", takahashi.Aliases);
        Assert.Contains(takahashi.Sources, source => source.Url == "https://rugbydb.tokyo/player/4334.html");
        Assert.DoesNotContain(catalog.Players, item => item.PlayerId == "rugbydb-138-4334");
    }

    [Fact]
    public void Corrected_club_history_uses_only_sourced_periods()
    {
        var catalog = LoadBundledCatalog();
        var ochi = Assert.Single(catalog.Players, player => player.NameJa == "落 和史");
        Assert.Contains(ochi.TeamHistory, history => history.TeamName == "日本製鉄釜石シーウェイブス" &&
            history.FromSeason == "2023-24" && history.ToSeason == "2025-26");

        var shigematsu = Assert.Single(catalog.Players, player => player.NameJa == "繁松 哲大");
        Assert.Contains(shigematsu.TeamHistory, history => history.TeamName == "レッドハリケーンズ大阪" &&
            history.FromSeason == "2021-22" && history.ToSeason == "2021-22");
        Assert.Contains(shigematsu.TeamHistory, history => history.TeamName == "浦安D-Rocks" &&
            history.FromSeason == "2022-23" && history.ToSeason == "2025-26");

        var oyama = Assert.Single(catalog.Players, player => player.NameJa == "大山 祥平");
        Assert.Contains(oyama.TeamHistory, history => history.TeamName == "リコーブラックラムズ東京" &&
            history.FromSeason == "2023-24" && history.ToSeason == "2025-26");
        Assert.Contains(oyama.Sources, source => source.Url == "https://rugbydb.tokyo/player/4052.html" &&
            source.Fields.Contains("teamHistory"));
    }

    [Fact]
    public void Search_matches_names_aliases_positions_and_past_teams()
    {
        var player = new PlayerRecord
        {
            PlayerId = "player-1",
            CurrentTeamId = "team-current",
            CurrentTeamName = "花園近鉄ライナーズ",
            Division = "DIV2",
            NameJa = "山田 太郎",
            NameEn = "Taro Yamada",
            Aliases = ["タロウ"],
            Positions = ["CTB"],
            Schools = ["大阪大学"],
            TeamHistory =
            [
                new PlayerTeamHistory
                {
                    TeamId = "team-past",
                    TeamName = "浦安D-Rocks",
                    FromSeason = "2022-23",
                    ToSeason = "2024-25"
                }
            ],
            RepresentativeHistory =
            [
                new PlayerRepresentativeHistory { TeamName = "日本代表", Level = "Senior" }
            ]
        };

        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "たろう")));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "浦安D-Rocks")));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "ctb 日本代表")));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "大阪大学")));
    }

    [Fact]
    public void Search_scope_can_limit_matches_to_current_team_previous_team_or_school()
    {
        var player = new PlayerRecord
        {
            PlayerId = "player-1",
            CurrentTeamId = "team-current",
            CurrentTeamName = "花園近鉄ライナーズ",
            NameJa = "山田 太郎",
            Schools = ["大阪大学"],
            TeamHistory =
            [
                new PlayerTeamHistory { TeamId = "team-past", TeamName = "浦安D-Rocks", FromSeason = "2022-23", ToSeason = "2024-25" },
                new PlayerTeamHistory { TeamId = "team-current", TeamName = "花園近鉄ライナーズ", FromSeason = "2025-26" }
            ]
        };

        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "花園", PlayerSearchScope.CurrentTeam)));
        Assert.Empty(PlayerSearch.Search([player], "浦安", PlayerSearchScope.CurrentTeam));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "浦安", PlayerSearchScope.PreviousTeam)));
        Assert.Empty(PlayerSearch.Search([player], "花園", PlayerSearchScope.PreviousTeam));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "大阪", PlayerSearchScope.School)));
        Assert.Empty(PlayerSearch.Search([player], "山田", PlayerSearchScope.School));
    }

    [Fact]
    public void Search_with_empty_query_returns_all_players()
    {
        var players = new[]
        {
            new PlayerRecord { PlayerId = "one", NameJa = "選手一" },
            new PlayerRecord { PlayerId = "two", NameJa = "選手二" }
        };

        Assert.Equal(2, PlayerSearch.Search(players, "  ").Count);
    }

    [Fact]
    public void Parser_rejects_duplicate_ids_and_unsafe_portrait_paths()
    {
        const string duplicateIds = """
        {
          "schemaVersion": 1,
          "season": "2026-27",
          "players": [
            { "playerId": "same", "currentTeamId": "team", "currentTeamName": "Team", "nameJa": "A" },
            { "playerId": "same", "currentTeamId": "team", "currentTeamName": "Team", "nameJa": "B" }
          ]
        }
        """;
        const string unsafePortrait = """
        {
          "schemaVersion": 1,
          "season": "2026-27",
          "players": [
            {
              "playerId": "one",
              "currentTeamId": "team",
              "currentTeamName": "Team",
              "nameJa": "A",
              "portrait": { "assetPath": "../outside.jpg" }
            }
          ]
        }
        """;

        Assert.False(PlayerCatalogParser.TryParse(duplicateIds, out _, out _));
        Assert.False(PlayerCatalogParser.TryParse(unsafePortrait, out _, out _));
    }

    [Fact]
    public void Parser_rejects_null_collections_without_throwing()
    {
        const string json = """{"schemaVersion":1,"season":"2026-27","players":null}""";
        Assert.False(PlayerCatalogParser.TryParse(json, out _, out var errors));
        Assert.Contains(errors, error => error.Contains("Players"));
    }

    [Fact]
    public void Parser_rejects_null_or_malformed_nested_collections_without_throwing()
    {
        var valid = ValidPlayerJson();
        var malformed = new[]
        {
            valid.Replace("\"aliases\": []", "\"aliases\": null", StringComparison.Ordinal),
            valid.Replace("\"positions\": []", "\"positions\": null", StringComparison.Ordinal),
            valid.Replace("\"schools\": []", "\"schools\": null", StringComparison.Ordinal),
            valid.Replace("\"teamHistory\": []", "\"teamHistory\": null", StringComparison.Ordinal),
            valid.Replace("\"teamHistory\": []", "\"teamHistory\": [null]", StringComparison.Ordinal),
            valid.Replace("\"representativeHistory\": []", "\"representativeHistory\": null", StringComparison.Ordinal),
            valid.Replace("\"representativeHistory\": []", "\"representativeHistory\": [null]", StringComparison.Ordinal),
            valid.Replace("\"representativeHistory\": []", "\"representativeHistory\": [{\"teamName\":\"Japan\",\"seasons\":null}]", StringComparison.Ordinal),
            valid.Replace("\"representativeHistory\": []", "\"representativeHistory\": [{\"teamName\":\"Japan\",\"seasons\":[null]}]", StringComparison.Ordinal),
            valid.Replace("\"fields\": []", "\"fields\": null", StringComparison.Ordinal),
            valid.Replace("\"fields\": []", "\"fields\": [null]", StringComparison.Ordinal),
            valid.Replace("\"sources\": [", "\"sources\": null, \"ignoredSources\": [", StringComparison.Ordinal),
            valid.Replace("\"teamHistory\": []", "\"teamHistory\": [{\"teamId\":\"team-1\",\"teamName\":\"Club\",\"fromSeason\":\"2024-26\",\"toSeason\":\"2025-26\"}]", StringComparison.Ordinal)
        };

        for (var i = 0; i < malformed.Length; i++)
            Assert.False(PlayerCatalogParser.TryParse(malformed[i], out _, out _), $"Malformed case {i} was accepted.");
    }

    [Fact]
    public void Search_handles_null_collections_and_null_nested_rows_defensively()
    {
        var player = new PlayerRecord
        {
            PlayerId = "defensive",
            NameJa = "確認対象",
            Aliases = null!,
            Positions = null!,
            Schools = null!,
            TeamHistory = [null!],
            RepresentativeHistory = [null!]
        };

        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "確認対象")));
        Assert.Empty(PlayerSearch.Search([player], "釜石", PlayerSearchScope.PreviousTeam));
    }

    [Fact]
    public void Duplicate_detector_reports_candidates_without_merging_them()
    {
        var first = new PlayerRecord
        {
            PlayerId = "one", CurrentTeamId = "team", NameJa = "山田・太郎", LeagueOnePlayerId = "123",
            BirthDate = new DateOnly(2000, 1, 1), HeightCm = 180, WeightKg = 90, Positions = ["PR"]
        };
        var second = new PlayerRecord
        {
            PlayerId = "two", CurrentTeamId = "team", NameJa = "山田 太郎", LeagueOnePlayerId = "123",
            BirthDate = new DateOnly(2000, 1, 1), HeightCm = 180, WeightKg = 90, Positions = ["PR / HO"]
        };

        var candidates = PlayerCatalogDuplicateDetector.FindCandidates([first, second]);
        var candidate = Assert.Single(candidates);
        Assert.Contains("同一チーム内の正規化氏名・別表記一致", candidate.Reasons);
        Assert.Contains("同じリーグワン公式選手ID", candidate.Reasons);
        Assert.Contains("生年月日と身長・体重・ポジションの複数一致", candidate.Reasons);
        Assert.NotSame(candidate.First, candidate.Second);
    }

    [Fact]
    public void Duplicate_detector_reports_shared_alias_and_english_name_candidates()
    {
        var first = new PlayerRecord
        {
            PlayerId = "one", CurrentTeamId = "team", NameJa = "山田 太郎", NameEn = "TARO YAMADA",
            Aliases = ["山田たろう"]
        };
        var aliasMatch = new PlayerRecord
        {
            PlayerId = "two", CurrentTeamId = "team", NameJa = "山田 太朗", Aliases = ["山田 太郎"]
        };
        var englishMatch = new PlayerRecord
        {
            PlayerId = "three", CurrentTeamId = "team", NameJa = "別人名", NameEn = "Taro Yamada"
        };

        var candidates = PlayerCatalogDuplicateDetector.FindCandidates([first, aliasMatch, englishMatch]);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, item => item.First.PlayerId == "one" && item.Second.PlayerId == "two" &&
            item.Reasons.Contains("同一チーム内の正規化氏名・別表記一致"));
        Assert.Contains(candidates, item => item.First.PlayerId == "one" && item.Second.PlayerId == "three" &&
            item.Reasons.Contains("同一チーム内の正規化氏名・別表記一致"));
        Assert.Equal(new[] { "one", "two", "three" }, new[] { first, aliasMatch, englishMatch }.Select(item => item.PlayerId));
    }

    [Fact]
    public void Parser_accepts_a_valid_empty_catalog()
    {
        const string json = """{"schemaVersion":1,"season":"2026-27","players":[]}""";

        Assert.True(PlayerCatalogParser.TryParse(json, out var catalog, out _));
        Assert.Equal("2026-27", catalog.Season);
        Assert.Empty(catalog.Players);
    }

    private static PlayerCatalogDocument LoadBundledCatalog()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        Assert.True(PlayerCatalogParser.TryParse(json, out var catalog, out var errors), string.Join(Environment.NewLine, errors));
        return catalog;
    }

    private static string ValidPlayerJson() => """
    {
      "schemaVersion": 1,
      "season": "2026-27",
      "players": [{
        "playerId": "one",
        "currentTeamId": "team-1",
        "currentTeamName": "Club",
        "division": "DIV1",
        "nameJa": "Player",
        "aliases": [],
        "positions": [],
        "schools": [],
        "teamHistory": [],
        "representativeHistory": [],
        "sources": [{ "publisher": "Source", "url": "https://example.com", "fields": [] }]
      }]
    }
    """;
}
