using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class ScheduleParserTests
{
    [Theory]
    [InlineData("12月19or20日", "土or日")]
    [InlineData("4月30日or5月1or2日", "土or日or祝")]
    [InlineData("1月30or31日2月6or7日", "土or日")]
    public void Preserves_ambiguous_multi_month_dates(string rawDate, string rawWeekday)
    {
        var html = $"""
            <table><tr><th>節</th><th>日程</th><th>曜日</th><th>キックオフ時間</th><th>ホストチーム VS ビジターチーム</th><th>開催会場</th></tr>
            <tr><td>第2節</td><td>{rawDate}</td><td>{rawWeekday}</td><td>未定</td>
            <td><div class="schedule-team"><div>チームA</div><div>v</div><div>チームB</div></div></td><td>未定</td></tr></table>
            """;

        var match = Assert.Single(ScheduleTableParser.Parse(
            html, "2026", "D2", "https://league-one.jp/content/schedule_table/2026/div2"));

        Assert.Contains(rawDate, match.Date, StringComparison.Ordinal);
        Assert.Contains(rawWeekday, match.Date, StringComparison.Ordinal);
    }

    [Fact]
    public void Preserves_ambiguous_date_and_current_official_team_names()
    {
        const string html = """
            <table class="schedule-table">
              <thead><tr><th>節</th><th colspan="2">日程</th><th>キックオフ時間</th><th>ホストチーム VS ビジターチーム</th><th colspan="2">開催会場</th></tr></thead>
              <tbody><tr>
                <td>第2節</td><td>12月19or20日</td><td>土or日</td><td>未定</td>
                <td><div class="schedule-team"><div>リコーブラックラムズ東京</div><div>v</div><div>栃木ホンダヒート</div></div></td>
                <td>東京</td><td>秩父宮</td>
              </tr></tbody>
            </table>
            """;

        var result = ScheduleTableParser.Parse(
            html,
            "2026",
            "D1",
            "https://league-one.jp/content/schedule_table/2026/div1");

        var match = Assert.Single(result);
        Assert.Equal("2026", match.SeasonKey);
        Assert.Equal("D1", match.CategoryCode);
        Assert.Equal("第2節", match.Round);
        Assert.Contains("12月19or20日", match.Date, StringComparison.Ordinal);
        Assert.Contains("土or日", match.Date, StringComparison.Ordinal);
        Assert.Equal("リコーブラックラムズ東京", match.Home);
        Assert.Equal("栃木ホンダヒート", match.Away);
        Assert.Equal("未定", match.Kickoff);
        Assert.Equal("試合前", match.Status);
        Assert.Equal("", match.Conference);
    }

    [Fact]
    public void Reports_official_index_vs_division_schedule_conflict_without_rewriting_schedule()
    {
        var d3Fixture = new ScheduleFetcher.Item { Home = "川越狭山セコムラガッツ", Away = "中国電力レッドレグリオンズ" };
        var d2Fixture = new ScheduleFetcher.Item { Home = "川越狭山セコムラガッツ", Away = "マツダスカイアクティブズ広島" };

        var currentTeams = SeasonCatalog.Teams2026
            .Select(team => new TeamIndexEntry(team.LeagueOneTeamId, team.TeamName, team.DivisionCode, team.TeamUrl, team.LogoUrl))
            .ToArray();
        var warning = ScheduleFetcher.BuildDivisionConsistencyWarning(
            Array.Empty<ScheduleFetcher.Item>(),
            new[] { d2Fixture },
            new[] { d3Fixture },
            currentTeams);

        Assert.Contains("川越狭山セコムラガッツ", warning, StringComparison.Ordinal);
        Assert.Equal("川越狭山セコムラガッツ", d2Fixture.Home);
    }
}
