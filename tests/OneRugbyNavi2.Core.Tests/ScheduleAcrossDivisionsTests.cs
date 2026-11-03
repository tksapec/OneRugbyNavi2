using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class ScheduleAcrossDivisionsTests
{
    [Fact]
    public void All_division_favorite_schedule_merges_d1_to_d3_in_date_order()
    {
        var model = new ScheduleViewModel();
        model.SetItems(
            [Match("d1-later", 1, "浦安D-Rocks", 5)],
            [Match("d2-earlier", 2, "花園近鉄ライナーズ", 2)],
            [Match("d3-middle", 3, "ヤクルトレビンズ戸田", 3)]);
        model.FavoriteTeamFilters = ["浦安D-Rocks", "花園近鉄ライナーズ", "ヤクルトレビンズ戸田"];
        model.ShowFavoritesAcrossDivisions = true;

        model.ApplyFilters();

        Assert.Equal(["d2-earlier", "d3-middle", "d1-later"], model.FilteredItems.Select(match => match.MatchId));
    }

    [Fact]
    public void Next_match_is_the_earliest_future_match_in_the_current_filtered_schedule()
    {
        var model = new ScheduleViewModel();
        var now = new DateTime(2026, 12, 1, 12, 0, 0);
        model.SetItems(
        [
            MatchAt("later", 1, "浦安D-Rocks", new DateTime(2026, 12, 4, 15, 0, 0)),
            MatchAt("next", 1, "浦安D-Rocks", new DateTime(2026, 12, 2, 15, 0, 0)),
            MatchAt("past", 1, "浦安D-Rocks", new DateTime(2026, 11, 30, 15, 0, 0))
        ], [], []);

        Assert.Equal("next", model.GetNextMatch(now)?.MatchId);
    }

    private static ScheduleFetcher.Item Match(string id, int division, string team, int days) => MatchAt(
        id, division, team, DateTime.Today.AddDays(days).AddHours(15));

    private static ScheduleFetcher.Item MatchAt(string id, int division, string team, DateTime start) => new()
    {
        SeasonKey = "2026",
        SeasonLabel = "2026-27シーズン",
        SeasonStartYear = 2026,
        CategoryCode = $"D{division}",
        Division = $"D{division}",
        Round = "第1節",
        Date = $"{start.Month}月{start.Day}日({"日月火水木金土"[(int)start.DayOfWeek]})",
        Kickoff = start.ToString("HH:mm"),
        Home = team,
        Away = $"対戦相手{division}",
        MatchId = id
    };
}
