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
        Assert.Equal(21, playersByTeam.Length);
        Assert.Equal(catalog.Players.Count, catalog.Players.Select(player => player.PlayerId).Distinct().Count());

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
    public void Parser_accepts_a_valid_empty_catalog()
    {
        const string json = """{"schemaVersion":1,"season":"2026-27","players":[]}""";

        Assert.True(PlayerCatalogParser.TryParse(json, out var catalog, out _));
        Assert.Equal("2026-27", catalog.Season);
        Assert.Empty(catalog.Players);
    }
}
