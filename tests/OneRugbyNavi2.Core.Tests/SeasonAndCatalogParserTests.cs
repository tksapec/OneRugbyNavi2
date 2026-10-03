using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class SeasonAndCatalogParserTests
{
    [Theory]
    [InlineData("D1", "div1")]
    [InlineData("d1", "div1")]
    [InlineData("D2", "div2")]
    [InlineData("d2", "div2")]
    [InlineData("D3", "div3")]
    [InlineData("d3", "div3")]
    public void Schedule_table_url_accepts_fetcher_division_codes(string division, string expectedSlug)
    {
        Assert.Equal(
            $"https://league-one.jp/content/schedule_table/2026/{expectedSlug}",
            SeasonCatalog.ScheduleTableUrl("2026", division));
    }

    [Theory]
    [InlineData("<h2>2026-27シーズン</h2>", 2026)]
    [InlineData("<h2>2025-26シーズン</h2>", 2025)]
    [InlineData("<div>2026ｰ27 シーズン</div>", 2026)]
    [InlineData("<div>2026‐27シーズン</div>", 2026)]
    public void Detect_returns_season_start_year(string html, int expected)
    {
        Assert.Equal(expected, LeagueOneSeasonDetector.Detect(html));
    }

    [Fact]
    public void Detect_returns_zero_when_no_concrete_season_is_present()
    {
        Assert.Equal(0, LeagueOneSeasonDetector.Detect("<html><body>チーム一覧</body></html>"));
    }

    [Fact]
    public void Parse_extracts_team_membership_by_division()
    {
        const string html = """
            <html><body>
              <h2>DIVISION 1</h2>
              <ul><li><a href="https://example.com/a">浦安D-Rocks公式サイト</a></li></ul>
              <h2>DIVISION 2</h2>
              <ul><li><a href="https://example.com/b">JR東日本グリーンウォリアーズ東葛公式サイト</a></li></ul>
              <h2>DIVISION 3</h2>
              <ul>
                <li><a href="https://example.com/c">丸和MOMOTARO’S成田公式サイト</a></li>
                <li><a href="https://example.com/c2">丸和MOMOTARO’S成田公式サイト</a></li>
              </ul>
            </body></html>
            """;

        var teams = TeamCatalogParser.Parse(html, 2026, "https://league-one.jp/content/team/2026");

        Assert.Collection(
            teams,
            team =>
            {
                Assert.Equal("浦安D-Rocks", team.TeamName);
                Assert.Equal("DIV1", team.DivisionCode);
            },
            team =>
            {
                Assert.Equal("JR東日本グリーンウォリアーズ東葛", team.TeamName);
                Assert.Equal("DIV2", team.DivisionCode);
            },
            team =>
            {
                Assert.Equal("丸和MOMOTARO’S成田", team.TeamName);
                Assert.Equal("DIV3", team.DivisionCode);
            });
    }

    [Fact]
    public void Parse_returns_empty_for_empty_document()
    {
        Assert.Empty(TeamCatalogParser.Parse("", 2026, "https://league-one.jp/content/team/2026"));
    }

    [Fact]
    public void Team_index_reads_season_ids_names_divisions_and_logos_from_current_markup()
    {
        const string html = """
            <html><head><title>公式 チーム一覧（2026-27）</title></head><body>
              <section id="division-1"><h2>DIVISION 1</h2><ul>
                <li><a href="/team/131"><img src="https://cdn.example/logo.png" alt="栃木ホンダヒート"><span class="name">栃木ホンダヒート</span></a></li>
              </ul></section>
              <section id="division-2"><h2>DIVISION 2</h2><ul>
                <li><a href="/team/137"><img src="/logo/blue.png" alt="清水建設江東ブルーシャークス"><span class="name">清水建設江東ブルーシャークス</span></a></li>
              </ul></section>
              <section id="division-3"><h2>DIVISION 3</h2><ul>
                <li><a href="/team/149"><img src="/logo/momotaro.png" alt="丸和MOMOTARO’S成田"><span class="name">丸和MOMOTARO’S成田</span></a></li>
              </ul></section>
            </body></html>
            """;

        var result = TeamIndexParser.Parse(html, 2026, "https://league-one.jp/team/");

        Assert.Equal(SyncReadiness.Ready, result.Readiness);
        Assert.Equal(2026, result.DetectedSeasonStartYear);
        Assert.Collection(result.Teams,
            d1 =>
            {
                Assert.Equal("131", d1.LeagueOneTeamId);
                Assert.Equal("栃木ホンダヒート", d1.TeamName);
                Assert.Equal("DIV1", d1.DivisionCode);
                Assert.Equal("https://league-one.jp/team/131", d1.TeamUrl);
                Assert.Equal("https://cdn.example/logo.png", d1.LogoUrl);
            },
            d2 =>
            {
                Assert.Equal("137", d2.LeagueOneTeamId);
                Assert.Equal("DIV2", d2.DivisionCode);
                Assert.Equal("https://league-one.jp/logo/blue.png", d2.LogoUrl);
            },
            d3 =>
            {
                Assert.Equal("149", d3.LeagueOneTeamId);
                Assert.Equal("DIV3", d3.DivisionCode);
            });
    }

    [Theory]
    [InlineData("栃木ホンダヒート", "三重ホンダヒート")]
    [InlineData("JR東日本グリーンウォリアーズ東葛", "NECグリーンロケッツ東葛")]
    [InlineData("川越狭山セコムラガッツ", "狭山セコムラガッツ")]
    [InlineData("丸和MOMOTARO’S成田", "AZ-COM丸和MOMOTARO’S")]
    public void Matches_historical_brands_for_new_current_team_names(string currentName, string oldName)
    {
        Assert.True(SeasonCatalog.AreSameTeamName(currentName, oldName, 2026));
        Assert.False(SeasonCatalog.AreSameTeamName(currentName, oldName, 2025));
        Assert.Equal(oldName, SeasonCatalog.NormalizeTeamName(oldName, 2026));
    }

    [Theory]
    [InlineData("埼玉パナソニックワイルドナイツ", "埼玉ワイルドナイツ")]
    [InlineData("東京サントリーサンゴリアス", "東京サンゴリアス")]
    [InlineData("リコーブラックラムズ東京", "ブラックラムズ東京")]
    [InlineData("マツダスカイアクティブズ広島", "スカイアクティブズ広島")]
    [InlineData("JR東日本グリーンウォリアーズ東葛", "グリーンウォリアーズ東葛")]
    public void Matches_source_specific_team_aliases_without_mutating_official_names(string officialName, string resultsName)
    {
        Assert.True(SeasonCatalog.AreSameTeamName(officialName, resultsName, 2026));
        Assert.False(SeasonCatalog.AreSameTeamName(officialName, resultsName, 2025));
        Assert.Equal(resultsName, SeasonCatalog.NormalizeTeamName(resultsName, 2026));
    }

    [Fact]
    public void Current_2026_catalog_matches_official_27_team_division_counts()
    {
        Assert.Equal(27, SeasonCatalog.Teams2026.Count);
        Assert.Equal(12, SeasonCatalog.Teams2026.Count(team => team.DivisionCode == "DIV1"));
        Assert.Equal(8, SeasonCatalog.Teams2026.Count(team => team.DivisionCode == "DIV2"));
        Assert.Equal(7, SeasonCatalog.Teams2026.Count(team => team.DivisionCode == "DIV3"));
        Assert.Equal("DIV2", SeasonCatalog.GetCurrentTeam("川越狭山セコムラガッツ")?.DivisionCode);
        Assert.Equal(27, SeasonCatalog.Teams2026.Select(team => team.LeagueOneTeamId).Distinct().Count());
    }
}
