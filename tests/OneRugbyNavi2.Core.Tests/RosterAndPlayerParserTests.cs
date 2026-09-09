using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class RosterAndPlayerParserTests
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

    [Fact]
    public void Team_roster_rejects_previous_season_without_players()
    {
        const string html = """
            <html><body>
              <h2>2025-26シーズンディビジョン2 レッドハリケーンズ大阪</h2>
              <table><tr><td><a href="/player/483965">渡邉 隆之</a></td></tr></table>
            </body></html>
            """;

        var result = TeamRosterParser.Parse(
            html,
            2026,
            "https://league-one.jp/team/116?t1=1",
            "レッドハリケーンズ大阪");

        Assert.Equal(SyncReadiness.NotPublished, result.Readiness);
        Assert.Empty(result.Players);
    }

    [Fact]
    public void Team_roster_parses_current_player_row_and_preserves_new_category_text()
    {
        const string html = """
            <html><body>
              <h2>2026-27シーズンディビジョン2 レッドハリケーンズ大阪</h2>
              <div>大阪府大阪市</div>
              <table>
                <tr>
                  <th>顔写真</th><th>選手名</th><th>ポジション</th><th>身長 (cm)</th><th>体重 (kg)</th><th>生年月日</th><th>登録区分</th>
                </tr>
                <tr>
                  <td><img src="https://assets.via-cloudflare.site/player/483965.jpg" /></td>
                  <td><a href="/player/483965">渡邉 隆之</a></td>
                  <td>PR （プロップ）</td>
                  <td>180cm</td>
                  <td>117kg</td>
                  <td>1994年05月27日</td>
                  <td>カテゴリA-1</td>
                </tr>
              </table>
            </body></html>
            """;

        var result = TeamRosterParser.Parse(
            html,
            2026,
            "https://league-one.jp/team/116?t1=1",
            "レッドハリケーンズ大阪");

        Assert.Equal(SyncReadiness.Ready, result.Readiness);
        Assert.Equal("レッドハリケーンズ大阪", result.TeamName);
        var player = Assert.Single(result.Players);
        Assert.Equal("483965", player.LeagueOnePlayerId);
        Assert.Equal("https://league-one.jp/player/483965", player.ProfileUrl);
        Assert.Equal("渡邉 隆之", player.NameJa);
        Assert.Equal("PR", player.PositionCode);
        Assert.Equal("プロップ", player.PositionName);
        Assert.Equal(180, player.HeightCm);
        Assert.Equal(117, player.WeightKg);
        Assert.Equal("1994-05-27", player.BirthDate);
        Assert.Equal("カテゴリA-1", player.RegistrationCategory);
        Assert.Equal("https://assets.via-cloudflare.site/player/483965.jpg", player.ImageUrl);
    }

    [Fact]
    public void Player_profile_rejects_previous_season()
    {
        const string html = """
            <html><body>
              <div>2025-26シーズン</div>
              <div>Takayuki Watanabe</div>
              <h1>渡邉 隆之 (PR)</h1>
              <div>リーグワンキャップ数：12</div>
            </body></html>
            """;

        var result = PlayerProfileParser.Parse(html, 2026, "https://league-one.jp/player/483965");

        Assert.Equal(SyncReadiness.NotPublished, result.Readiness);
        Assert.Null(result.Profile);
    }

    [Fact]
    public void Player_profile_parses_detail_fields()
    {
        const string html = """
            <html><body>
              <div>2026-27シーズン</div>
              <div class="name-en">Takayuki Watanabe</div>
              <h1>渡邉 隆之 (PR)</h1>
              <dl>
                <dt>身長 / 体重</dt><dd>180cm / 117kg</dd>
                <dt>生年月日</dt><dd>1994.05.27 (32歳)</dd>
                <dt>出身校・チーム歴</dt><dd>札幌山の手高校 東海大学</dd>
                <dt>登録区分</dt><dd>カテゴリA-1</dd>
                <dt>リーグワンキャップ数</dt><dd>12</dd>
              </dl>
            </body></html>
            """;

        var result = PlayerProfileParser.Parse(html, 2026, "https://league-one.jp/player/483965");

        Assert.Equal(SyncReadiness.Ready, result.Readiness);
        Assert.NotNull(result.Profile);
        Assert.Equal("483965", result.Profile!.LeagueOnePlayerId);
        Assert.Equal("渡邉 隆之", result.Profile.NameJa);
        Assert.Equal("Takayuki Watanabe", result.Profile.NameEn);
        Assert.Equal("PR", result.Profile.PositionCode);
        Assert.Equal(180, result.Profile.HeightCm);
        Assert.Equal(117, result.Profile.WeightKg);
        Assert.Equal("1994-05-27", result.Profile.BirthDate);
        Assert.Equal("札幌山の手高校 東海大学", result.Profile.SchoolTeamHistoryText);
        Assert.Equal("カテゴリA-1", result.Profile.RegistrationCategory);
        Assert.Equal(12, result.Profile.LeagueOneCaps);
    }
}
