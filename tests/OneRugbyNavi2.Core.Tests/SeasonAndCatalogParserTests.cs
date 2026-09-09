using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class SeasonAndCatalogParserTests
{
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
}
