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
              <a href="/team/116">レッドハリケーンズ大阪</a>
              <a href="https://league-one.jp/team/107?t1=1">横浜キヤノンイーグルス</a>
              <a href="https://example.com/team/999">外部サイト</a>
            </body></html>
            """;

        var result = TeamIndexParser.Parse(html, 2026, "https://league-one.jp/team/");

        Assert.Equal(SyncReadiness.Ready, result.Readiness);
        Assert.Collection(
            result.Teams,
            team =>
            {
                Assert.Equal("116", team.LeagueOneTeamId);
                Assert.Equal("レッドハリケーンズ大阪", team.TeamName);
                Assert.Equal("https://league-one.jp/team/116", team.TeamUrl);
            },
            team =>
            {
                Assert.Equal("107", team.LeagueOneTeamId);
                Assert.Equal("https://league-one.jp/team/107?t1=1", team.TeamUrl);
            });
    }

}
