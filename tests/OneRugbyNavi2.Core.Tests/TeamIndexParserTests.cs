using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class TeamIndexParserTests
{
    [Fact]
    public void Team_index_rejects_previous_season()
    {
        const string html = """
            <html><body>
              <h2>2025-26シーズン</h2>
              <a href="/team/116">レッドハリケーンズ大阪</a>
            </body></html>
            """;

        var result = TeamIndexParser.Parse(html, 2026, "https://league-one.jp/team/");

        Assert.Equal(SyncReadiness.NotPublished, result.Readiness);
        Assert.Equal(2025, result.DetectedSeasonStartYear);
        Assert.Empty(result.Teams);
    }

    [Fact]
    public void Team_index_accepts_only_internal_team_links()
    {
        const string html = """
            <html><body>
              <h2>2026-27シーズン</h2>
              <section id="division-2"><h2>DIVISION 2</h2>
                <a href="/team/116"><span class="name">レッドハリケーンズ大阪</span></a>
              </section>
              <section id="division-1"><h2>DIVISION 1</h2>
                <a href="https://league-one.jp/team/107?t1=1"><span class="name">横浜キヤノンイーグルス</span></a>
                <a href="https://example.com/team/999"><span class="name">外部サイト</span></a>
              </section>
            </body></html>
            """;

        var result = TeamIndexParser.Parse(html, 2026, "https://league-one.jp/team/");

        Assert.Equal(SyncReadiness.Ready, result.Readiness);
        Assert.Collection(
            result.Teams,
            team =>
            {
                Assert.Equal("116", team.LeagueOneTeamId);
                Assert.Equal("DIV2", team.DivisionCode);
                Assert.Equal("レッドハリケーンズ大阪", team.TeamName);
                Assert.Equal("https://league-one.jp/team/116", team.TeamUrl);
            },
            team =>
            {
                Assert.Equal("107", team.LeagueOneTeamId);
                Assert.Equal("DIV1", team.DivisionCode);
                Assert.Equal("https://league-one.jp/team/107?t1=1", team.TeamUrl);
            });
    }

    [Fact]
    public void Current_official_team_index_reads_external_team_links_and_league_logos()
    {
        const string html = """
            <html><body><h1>チーム一覧 2026-27シーズン</h1>
              <section class="division" id="division-2"><h2>DIVISION 2</h2><ul>
                <li><a href="https://urayasu-d-rocks.com/">
                  <img src="https://league-one.s3.ap-northeast-1.amazonaws.com/image/menu/126.png" alt="浦安D-Rocks" />
                  <span class="name">浦安D-Rocks</span>
                </a></li>
              </ul></section>
            </body></html>
            """;

        var result = TeamIndexParser.Parse(html, 2026, "https://league-one.jp/content/team/2026");

        var team = Assert.Single(result.Teams);
        Assert.Equal(SyncReadiness.Ready, result.Readiness);
        Assert.Equal("126", team.LeagueOneTeamId);
        Assert.Equal("DIV2", team.DivisionCode);
        Assert.Equal("https://league-one.jp/team/126", team.TeamUrl);
        Assert.Equal("https://league-one.s3.ap-northeast-1.amazonaws.com/image/menu/126.png", team.LogoUrl);
    }

}
