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
        Assert.DoesNotContain(catalog.Players.SelectMany(player => player.Aliases), alias =>
            System.Text.RegularExpressions.Regex.IsMatch(alias ?? "", @"^\d+$"));
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
    public void Bundled_team_history_is_normalized_and_school_university_club_ordered()
    {
        var catalog = LoadBundledCatalog();

        foreach (var player in catalog.Players)
        {
            var expected = PlayerTeamHistoryDeduplicator.Normalize(player)
                .Select(HistoryFingerprint).ToArray();
            var actual = player.TeamHistory.Select(HistoryFingerprint).ToArray();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Reviewed_history_corrections_remove_duplicate_current_and_renamed_team_entries()
    {
        var catalog = LoadBundledCatalog();
        var ochi = Assert.Single(catalog.Players, player => player.NameJa == "落 和史");
        Assert.Contains(ochi.TeamHistory, history => history.TeamId == "884" &&
            history.TeamName == "日本製鉄釜石シーウェイブス" &&
            history.FromSeason == "2023-24" && history.ToSeason == "2025-26");
        Assert.Contains(ochi.Sources, source => source.Url == "https://league-one.jp/news/6091" &&
            source.Fields.Contains("teamHistory"));

        var shigematsu = Assert.Single(catalog.Players, player => player.NameJa == "繁松 哲大");
        Assert.Contains(shigematsu.TeamHistory, history => history.TeamName == "レッドハリケーンズ大阪" &&
            history.FromSeason == "2021-22" && history.ToSeason == "2021-22");
        Assert.Contains(shigematsu.TeamHistory, history => history.TeamName == "浦安D-Rocks" &&
            history.FromSeason == "2022-23" && history.ToSeason == "2025-26");
        Assert.DoesNotContain(shigematsu.TeamHistory, history => history.TeamName == "NTTドコモレッドハリケーンズ大阪");
        Assert.Contains(shigematsu.Sources, source => source.Url == "https://league-one.jp/news/6091" &&
            source.Fields.Contains("teamHistory"));

        var johnny = Assert.Single(catalog.Players, player => player.NameJa == "ジョニー・ファアウリ");
        Assert.Equal(100, johnny.WeightKg);
        Assert.Contains(johnny.TeamHistory, history => history.TeamName == "三重ホンダヒート" && history.FromSeason == "2024-25" && history.ToSeason == "2025-26");
        Assert.DoesNotContain(johnny.TeamHistory, history => history.TeamName == "栃木ホンダヒート");

        var oyama = Assert.Single(catalog.Players, player => player.NameJa == "大山 祥平");
        Assert.Equal(115, oyama.WeightKg);
        Assert.Contains(oyama.TeamHistory, history => history.TeamName == "リコーブラックラムズ東京" &&
            history.FromSeason == "2022-23" && history.ToSeason == "2025-26");

        var yamazaki = Assert.Single(catalog.Players, player => player.NameJa == "山崎 昇悟");
        Assert.Contains(yamazaki.Sources, source => source.Url == "https://league-one.jp/news/6091" &&
            source.Fields.Contains("positions") && source.Fields.Contains("registrationCategories"));

        foreach (var pair in new[]
        {
            ("109", "rugbydb:team:718"), ("105", "rugbydb:team:734"), ("118", "144"),
            ("116", "rugbydb:team:720"), ("142", "rugbydb:team:720"),
            ("127", "rugbydb:team:729"), ("124", "rugbydb:team:723"), ("145", "rugbydb:team:808")
        })
        {
            Assert.DoesNotContain(catalog.Players, player => HasOverlappingHistory(player, pair.Item1, pair.Item2));
        }
    }

    private static bool HasOverlappingHistory(PlayerRecord player, string firstTeamId, string secondTeamId)
    {
        var first = player.TeamHistory.Where(history => history.TeamId == firstTeamId).ToArray();
        var second = player.TeamHistory.Where(history => history.TeamId == secondTeamId).ToArray();
        return first.Any(left => second.Any(right =>
            Overlap(HistoryYears(left), HistoryYears(right))));
    }

    private static HashSet<int> HistoryYears(PlayerTeamHistory history)
    {
        static int? Year(string? season) => season?.Length >= 4 && int.TryParse(season.AsSpan(0, 4), out var value) ? value : null;
        var start = Year(history.FromSeason) ?? Year(history.ToSeason);
        var end = Year(history.ToSeason) ?? Year(history.FromSeason);
        return start is null || end is null ? [] : Enumerable.Range(start.Value, Math.Max(1, end.Value - start.Value + 1)).ToHashSet();
    }

    private static bool Overlap(HashSet<int> left, HashSet<int> right) => left.Overlaps(right);

    [Fact]
    public void Bundled_catalog_merges_reviewed_cross_source_player_duplicates_and_preserves_positions()
    {
        var catalog = LoadBundledCatalog();
        var duplicateIds = new[]
        {
            "rugbydb-123-13482", "rugbydb-129-38262", "rugbydb-138-26208",
            "rugbydb-143-32449", "rugbydb-146-34389", "rugbydb-149-11680", "rugbydb-149-19632"
        };
        Assert.DoesNotContain(catalog.Players, player => duplicateIds.Contains(player.PlayerId, StringComparer.Ordinal));

        var kauri = Assert.Single(PlayerSearch.Search(catalog.Players, "カウリ・ティペネグレース"));

        Assert.Equal("leagueone-143-%e3%82%ab%e3%82%a6%e3%83%aa%e3%83%bb%e3%83%86%e3%82%a3%e3%83%9a%e3%83%8d%e3%82%b0%e3%83%ac%e3%83%bc%e3%82%b9", kauri.PlayerId);
        Assert.Contains(kauri.Positions, position => position.Contains("FB", StringComparison.Ordinal));
        Assert.Contains(kauri.Positions, position => position.Contains("WTB", StringComparison.Ordinal));
        Assert.Contains(kauri.Sources, source => source.Url == "https://rugbydb.tokyo/player/32449.html");
        Assert.DoesNotContain(kauri.Aliases, alias => alias.Contains("32449", StringComparison.Ordinal));
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
        Assert.DoesNotContain("37805", dalton.Aliases);
        Assert.Contains(dalton.Sources, source => source.Url == "https://rugbydb.tokyo/player/37805.html");
        Assert.DoesNotContain(catalog.Players, item => item.PlayerId == "rugbydb-127-37805");

        var takahashi = Assert.Single(catalog.Players, item => item.LeagueOnePlayerId == "484434");
        Assert.Contains("高橋信之", takahashi.Aliases);
        Assert.DoesNotContain("4334", takahashi.Aliases);
        Assert.Contains(takahashi.Sources, source => source.Url == "https://rugbydb.tokyo/player/4334.html");
        Assert.DoesNotContain(catalog.Players, item => item.PlayerId == "rugbydb-138-4334");
    }

    [Fact]
    public void Corrected_club_history_uses_only_sourced_periods()
    {
        var catalog = LoadBundledCatalog();
        var maruyama = Assert.Single(catalog.Players, player => player.PlayerId == "leagueone-131-rintaro-maruyama");
        Assert.Contains(maruyama.TeamHistory, history => history.TeamName == "トヨタヴェルブリッツ" &&
            history.FromSeason == "2022-23" && history.ToSeason == "2023-24");
        Assert.Contains(maruyama.TeamHistory, history => history.TeamName == "花園近鉄ライナーズ" &&
            history.FromSeason == "2024-25" && history.ToSeason == "2025-26");
        Assert.Contains(maruyama.TeamHistory, history => history.TeamName == "栃木ホンダヒート" &&
            history.FromSeason == "2026-27" && history.Type == "club");
        Assert.Contains(maruyama.TeamHistory, history => history.TeamName == "東福岡高校" && history.Type == "highSchool");
        Assert.Contains(maruyama.TeamHistory, history => history.TeamName == "東海大学" && history.Type == "university");

        var waqa = Assert.Single(catalog.Players, player => player.NameEn == "Sanaila Waqa");
        Assert.Contains(waqa.TeamHistory, history => history.TeamName == "NECグリーンロケッツ" &&
            history.FromSeason == "2017-18" && history.ToSeason == "2019-20");
        var waqaKintetsu = Assert.Single(waqa.TeamHistory, history => history.Type == "club" &&
            history.TeamName == "花園近鉄ライナーズ");
        Assert.Equal("2020-21", waqaKintetsu.FromSeason);
        Assert.Equal(string.Empty, waqaKintetsu.ToSeason);

        var ochi = Assert.Single(catalog.Players, player => player.NameJa == "落 和史");
        Assert.Contains(ochi.TeamHistory, history => history.TeamName == "日本製鉄釜石シーウェイブス" &&
            history.FromSeason == "2023-24" && history.ToSeason == "2025-26");

        var shigematsu = Assert.Single(catalog.Players, player => player.NameJa == "繁松 哲大");
        Assert.Contains(shigematsu.TeamHistory, history => history.TeamName == "レッドハリケーンズ大阪" &&
            history.FromSeason == "2021-22" && history.ToSeason == "2021-22");
        Assert.DoesNotContain(shigematsu.TeamHistory, history => history.TeamName == "NTTドコモレッドハリケーンズ大阪");
        Assert.Contains(shigematsu.TeamHistory, history => history.TeamName == "浦安D-Rocks" &&
            history.FromSeason == "2022-23" && history.ToSeason == "2025-26");

        var oyama = Assert.Single(catalog.Players, player => player.NameJa == "大山 祥平");
        Assert.Contains(oyama.TeamHistory, history => history.TeamName == "リコーブラックラムズ東京" &&
            history.FromSeason == "2022-23" && history.ToSeason == "2025-26");
        Assert.Contains(oyama.Sources, source => source.Url == "https://rugbydb.tokyo/player/4052.html" &&
            source.Fields.Contains("teamHistory"));

        var yokoi = Assert.Single(catalog.Players, player => player.NameJa == "横井 隼");
        Assert.Contains(yokoi.TeamHistory, history => history.TeamId == "rugbydb:team:806" &&
            history.TeamName == "宗像サニックスブルーズ" && history.FromSeason == "2020-21" && history.ToSeason == "2020-21");
        Assert.Contains(yokoi.Sources, source => source.Url == "https://rugbydb.tokyo/player/3296.html" &&
            source.Fields.Contains("teamHistory"));
    }

    [Fact]
    public void Sakita_appears_once_with_official_positions_and_the_rugbydb_variant_as_an_alias()
    {
        var catalog = LoadBundledCatalog();
        var player = Assert.Single(catalog.Players, item => item.NameEn == "Raito Sakita");

        Assert.Contains(player.Positions, position => position.Contains("SO", StringComparison.Ordinal));
        Assert.Contains(player.Positions, position => position.Contains("FB", StringComparison.Ordinal));
        Assert.Contains("嶋田士人", player.Aliases);
        Assert.DoesNotContain(catalog.Players, item => item.PlayerId == "rugbydb-140-16680");
        Assert.Single(PlayerSearch.Search(catalog.Players, "嶋田士人"));
    }

    [Fact]
    public void Panasonic_player_positions_exclude_newcomer_badges_and_use_verified_positions()
    {
        var catalog = LoadBundledCatalog();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["leagueone-128-takata_kaito"] = "PR",
            ["leagueone-128-juan_oosthuyzen"] = "LO",
            ["leagueone-128-naramoto_kanjiro"] = "SO",
            ["leagueone-128-malakye_enasio"] = "CTB",
            ["leagueone-128-toshi_kurita_butlin"] = "WTB"
        };

        foreach (var (playerId, position) in expected)
        {
            var player = Assert.Single(catalog.Players, item => item.PlayerId == playerId);
            Assert.Equal(position, Assert.Single(player.Positions));
            Assert.DoesNotContain(player.Positions, item => item.StartsWith("NEW ", StringComparison.Ordinal));
        }

        var grassmann = Assert.Single(catalog.Players, item => item.PlayerId == "leagueone-141-wynand-grassmann");
        Assert.Equal("Wynand Grassmann", grassmann.NameEn);

        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["leagueone-140-peter_umaga_jensen"] = "ピーター・ウマガ＝ジェンセン",
            ["leagueone-141-wynand-grassmann"] = "ワイナンド・グラスマン",
            ["leagueone-141-aj-woulf"] = "AJ・ウルフ",
            ["rugbydb-137-9812"] = "オルビン・レジャー",
            ["rugbydb-146-50602"] = "クレイグランガ・テイン"
        };
        foreach (var (playerId, displayName) in displayNames)
            Assert.Equal(displayName, Assert.Single(catalog.Players, item => item.PlayerId == playerId).NameJa);
    }

    [Fact]
    public void Catalog_parses_nationalities_and_season_scoped_registration_categories()
    {
        const string json = """
        {"schemaVersion":1,"season":"2026-27","players":[{
          "playerId":"one","currentTeamId":"team-1","currentTeamName":"Club","nameJa":"選手",
          "nationalities":["New Zealand"],
          "registrationCategories":[{"season":"2025-26","category":"C"}],
          "sources":[{"publisher":"League One","url":"https://league-one.jp/player/1","checkedOn":"2026-10-10","fields":["nationalities","registrationCategories"]}]
        }]}
        """;

        Assert.True(PlayerCatalogParser.TryParse(json, out var catalog, out var errors), string.Join(Environment.NewLine, errors));
        var player = Assert.Single(catalog.Players);
        Assert.Equal("New Zealand", Assert.Single(player.Nationalities));
        var category = Assert.Single(player.RegistrationCategories);
        Assert.Equal("2025-26", category.Season);
        Assert.Equal("C", category.Category);
        Assert.Same(player, Assert.Single(PlayerSearch.Search(catalog.Players, "New Zealand")));
        Assert.Same(player, Assert.Single(PlayerSearch.Search(catalog.Players, "2025-26 C")));
    }

    [Fact]
    public void Legacy_transfer_list_adds_previous_club_and_last_season()
    {
        var catalog = LoadBundledCatalog();
        var boshier = Assert.Single(catalog.Players, player => player.NameEn == "Lachlan Boshier");

        Assert.Contains(boshier.TeamHistory, history => history.TeamName == "チーフス" &&
            history.Type == "club" && history.FromSeason == "" && history.ToSeason == "2020-21");
        Assert.Contains(boshier.Sources, source => source.Url == "https://rugbydb.tokyo/list/transfer21.html" &&
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
    public void Search_matches_known_season_and_ranges_but_not_undated_history()
    {
        var ranged = new PlayerRecord
        {
            PlayerId = "ranged", TeamHistory = [new PlayerTeamHistory { TeamId = "club", TeamName = "Club", FromSeason = "2022-23", ToSeason = "2024-25" }]
        };
        var undated = new PlayerRecord
        {
            PlayerId = "undated", TeamHistory = [new PlayerTeamHistory { TeamId = "club2", TeamName = "Other Club" }]
        };

        Assert.Same(ranged, Assert.Single(PlayerSearch.Search([ranged, undated], "2023-24")));
        Assert.Same(ranged, Assert.Single(PlayerSearch.Search([ranged, undated], "2022-23")));
        Assert.Empty(PlayerSearch.Search([ranged, undated], "2025-26"));
        Assert.Empty(PlayerSearch.Search([ranged], "2023-24", PlayerSearchScope.CurrentTeam));
    }

    [Fact]
    public void End_only_history_preserves_last_known_season_without_inventing_start()
    {
        var player = new PlayerRecord
        {
            PlayerId = "end-only",
            TeamHistory = [new PlayerTeamHistory { TeamId = "club", TeamName = "Club", ToSeason = "2020-21" }]
        };
        var json = ValidPlayerJson().Replace("\"teamHistory\": []",
            "\"teamHistory\": [{\"teamId\":\"club\",\"teamName\":\"Club\",\"toSeason\":\"2020-21\"}]", StringComparison.Ordinal);

        Assert.True(PlayerCatalogParser.TryParse(json, out _, out var errors), string.Join(Environment.NewLine, errors));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "2020-21")));
        Assert.Empty(PlayerSearch.Search([player], "2019-20"));
    }

    [Fact]
    public void Team_history_merges_overlapping_rows_for_the_same_club_but_keeps_separate_stints()
    {
        var player = new PlayerRecord
        {
            PlayerId = "history-duplicates",
            CurrentTeamId = "official-current-id",
            CurrentTeamName = "Current Club",
            TeamHistory =
            [
                new PlayerTeamHistory { TeamId = "official-current-id", TeamName = "Current Club", FromSeason = "2025-26" },
                new PlayerTeamHistory { TeamId = "rugbydb:team:1", TeamName = "Current Club", FromSeason = "2022-23", ToSeason = "2025-26" },
                new PlayerTeamHistory { TeamId = "rugbydb:team:2", TeamName = "Former Club", FromSeason = "2020-21", ToSeason = "2021-22" },
                new PlayerTeamHistory { TeamId = "former-alias", TeamName = "Former Club", FromSeason = "2024-25", ToSeason = "2024-25" },
                new PlayerTeamHistory { TeamId = "other", TeamName = "Current Club", Type = "university", FromSeason = "2018-19" }
            ]
        };

        var result = PlayerTeamHistoryDeduplicator.Normalize(player);

        Assert.Equal(4, result.Count);
        var current = Assert.Single(result, history => history.Type == "club" && history.TeamName == "Current Club");
        Assert.Equal("official-current-id", current.TeamId);
        Assert.Equal("2022-23", current.FromSeason);
        Assert.Equal("", current.ToSeason);
        Assert.Equal(2, result.Count(history => history.Type == "club" && history.TeamName == "Former Club"));
        Assert.Contains(result, history => history.Type == "university");
    }

    [Fact]
    public void Team_history_merges_adjacent_seasons_for_same_team_and_sorts_school_before_university_before_club()
    {
        var player = new PlayerRecord
        {
            PlayerId = "history-order",
            CurrentTeamId = "pro",
            CurrentTeamName = "Pro Club",
            TeamHistory =
            [
                new PlayerTeamHistory { TeamId = "pro", TeamName = "Pro Club", FromSeason = "2023-24" },
                new PlayerTeamHistory { TeamId = "hs", TeamName = "High School", Type = "highSchool", FromSeason = "2017-18", ToSeason = "2019-20" },
                new PlayerTeamHistory { TeamId = "pro-alias", TeamName = "Pro Club", FromSeason = "2021-22", ToSeason = "2022-23" },
                new PlayerTeamHistory { TeamId = "uni", TeamName = "University", Type = "university", FromSeason = "2020-21", ToSeason = "2020-21" }
            ]
        };

        var result = PlayerTeamHistoryDeduplicator.Normalize(player);

        Assert.Equal(new[] { "highSchool", "university", "club" }, result.Select(history => history.Type));
        var pro = Assert.Single(result, history => history.Type == "club");
        Assert.Equal("2021-22", pro.FromSeason);
        Assert.Equal("", pro.ToSeason);
    }

    [Fact]
    public void Team_history_display_groups_repeat_stints_once_and_orders_school_university_then_professional_teams()
    {
        var player = new PlayerRecord
        {
            PlayerId = "display-order",
            CurrentTeamId = "pro",
            CurrentTeamName = "Pro Club",
            TeamHistory =
            [
                new PlayerTeamHistory { TeamId = "pro", TeamName = "Pro Club", FromSeason = "2023-24" },
                new PlayerTeamHistory { TeamId = "old-club", TeamName = "Former Club", FromSeason = "2020-21", ToSeason = "2020-21" },
                new PlayerTeamHistory { TeamId = "hs", TeamName = "High School", Type = "highSchool" },
                new PlayerTeamHistory { TeamId = "uni", TeamName = "University", Type = "university", FromSeason = "2019-20" },
                new PlayerTeamHistory { TeamId = "pro-old", TeamName = "Pro Club", FromSeason = "2021-22", ToSeason = "2021-22" }
            ]
        };

        var groups = PlayerTeamHistoryDisplayGroup.Create(player);

        Assert.Equal(new[] { "highSchool", "university", "club", "club" }, groups.Select(group => group.Type));
        Assert.Equal("High School", groups[0].TeamName);
        Assert.Equal("University", groups[1].TeamName);
        var pro = Assert.Single(groups, group => group.TeamName == "Pro Club");
        Assert.Equal(2, pro.Stints.Count);
        Assert.Equal("2021-22 / 2023-24〜現在", pro.Periods);
    }

    [Fact]
    public void Team_history_display_keeps_end_only_period_without_inventing_start_or_duplicate_unknown_rows()
    {
        var player = new PlayerRecord
        {
            PlayerId = "end-only-display",
            CurrentTeamId = "current",
            CurrentTeamName = "Current Club",
            TeamHistory =
            [
                new PlayerTeamHistory { TeamId = "former", TeamName = "Former Club" },
                new PlayerTeamHistory { TeamId = "former-rugbydb", TeamName = "Former Club", ToSeason = "2020-21" }
            ]
        };

        var former = Assert.Single(PlayerTeamHistoryDisplayGroup.Create(player), group => group.TeamName == "Former Club");

        Assert.Equal("〜2020-21", former.Periods);
    }

    [Fact]
    public void Parser_defaults_missing_history_type_to_club_and_rejects_unknown_type()
    {
        var legacy = ValidPlayerJson().Replace("\"teamHistory\": []", "\"teamHistory\": [{\"teamId\":\"club\",\"teamName\":\"Club\"}]", StringComparison.Ordinal);
        Assert.True(PlayerCatalogParser.TryParse(legacy, out var catalog, out var errors), string.Join(Environment.NewLine, errors));
        Assert.Equal("club", Assert.Single(catalog.Players[0].TeamHistory).Type);

        var invalid = legacy.Replace("\"teamName\":\"Club\"", "\"teamName\":\"Club\",\"type\":\"school\"", StringComparison.Ordinal);
        Assert.False(PlayerCatalogParser.TryParse(invalid, out _, out _));
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
            valid.Replace("\"nationalities\": []", "\"nationalities\": null", StringComparison.Ordinal),
            valid.Replace("\"registrationCategories\": []", "\"registrationCategories\": null", StringComparison.Ordinal),
            valid.Replace("\"registrationCategories\": []", "\"registrationCategories\": [null]", StringComparison.Ordinal),
            valid.Replace("\"registrationCategories\": []", "\"registrationCategories\": [{\"season\":\"2025-26\",\"category\":\"D\"}]", StringComparison.Ordinal),
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
            Nationalities = null!,
            RegistrationCategories = null!,
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

    private static string HistoryFingerprint(PlayerTeamHistory history)
        => $"{history.TeamId}|{history.TeamName}|{history.Type}|{history.FromSeason}|{history.ToSeason}";

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
        "nationalities": [],
        "registrationCategories": [],
        "schools": [],
        "teamHistory": [],
        "representativeHistory": [],
        "sources": [{ "publisher": "Source", "url": "https://example.com", "fields": [] }]
      }]
    }
    """;
}
