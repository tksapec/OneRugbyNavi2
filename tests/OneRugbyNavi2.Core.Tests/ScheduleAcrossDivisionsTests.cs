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
    public void Favorite_filter_keeps_only_registered_teams_when_changing_category()
    {
        var model = new ScheduleViewModel();
        model.SetItems([Match("d1", 1, "Club A", 5)], [Match("d2", 2, "Club C", 5)], [],
            [Match("playoff", 2, "Club A", 5)], []);
        model.FavoriteTeamFilters = ["Club A"];
        model.SetSource(ScheduleViewModel.CategoryDiv2);
        Assert.Empty(model.FilteredItems);
        model.SetSource(ScheduleViewModel.CategoryReplacement);
        Assert.Equal("playoff", Assert.Single(model.FilteredItems).MatchId);
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

    [Fact]
    public void Next_match_can_use_a_known_date_when_kickoff_is_unknown()
    {
        var model = new ScheduleViewModel();
        var unknown = MatchAt("unknown", 1, "Club A", new DateTime(2026, 12, 2, 15, 0, 0));
        unknown.Kickoff = "未定";
        var later = MatchAt("later", 1, "Club A", new DateTime(2026, 12, 3, 12, 0, 0));
        model.SetItems([unknown, later], [], []);
        Assert.Equal("unknown", model.GetNextMatch(new DateTime(2026, 12, 1, 12, 0, 0))?.MatchId);
    }

    [Theory]
    [InlineData("中止", "valid")]
    [InlineData("延期・日程未定", "valid")]
    [InlineData("取消", "valid")]
    [InlineData("延期", "cancelled")]
    public void Next_match_skips_cancelled_or_indefinitely_postponed_fixtures(string status, string expectedMatchId)
    {
        var model = new ScheduleViewModel();
        var cancelled = MatchAt("cancelled", 1, "Club A", new DateTime(2026, 12, 2, 15, 0, 0));
        cancelled.Status = status;
        var valid = MatchAt("valid", 1, "Club A", new DateTime(2026, 12, 3, 15, 0, 0));
        model.SetItems([cancelled, valid], [], []);

        Assert.Equal(expectedMatchId, model.GetNextMatch(new DateTime(2026, 12, 1, 12, 0, 0))?.MatchId);
    }

    [Fact]
    public void Upcoming_filter_excludes_started_and_live_matches_but_keeps_unknown_time()
    {
        var now = new DateTime(2026, 12, 12, 14, 0, 0);
        MatchItem Match(string time, string status = "") => new() { SeasonStartYear = 2026, MatchDate = "12月12日", KickoffTime = time, MatchStatus = status };
        Assert.False(ScheduleViewModel.TryMatchPeriod(Match("13:00"), now, ScheduleViewModel.DateRangeFilter.Upcoming));
        Assert.False(ScheduleViewModel.TryMatchPeriod(Match("15:00", "LIVE"), now, ScheduleViewModel.DateRangeFilter.Upcoming));
        Assert.True(ScheduleViewModel.TryMatchPeriod(Match("15:00"), now, ScheduleViewModel.DateRangeFilter.Upcoming));
        Assert.True(ScheduleViewModel.TryMatchPeriod(Match("未定"), now, ScheduleViewModel.DateRangeFilter.Upcoming));
    }

    [Theory]
    [InlineData("中止", false)]
    [InlineData("延期・日程未定", false)]
    [InlineData("延期", true)]
    public void Upcoming_filter_excludes_cancelled_and_indefinitely_postponed_matches(string status, bool expected)
    {
        var match = new MatchItem
        {
            SeasonStartYear = 2026,
            MatchDate = "12月13日",
            KickoffTime = "15:00",
            MatchStatus = status
        };

        Assert.Equal(expected, ScheduleViewModel.TryMatchPeriod(match, new DateTime(2026, 12, 12, 12, 0, 0), ScheduleViewModel.DateRangeFilter.Upcoming));
    }

    [Theory]
    [InlineData("13:00or14:00")]
    [InlineData("25:90")]
    [InlineData("12:60")]
    [InlineData("未定")]
    public void Ambiguous_or_invalid_kickoff_is_not_a_match_start(string kickoff)
    {
        var match = new MatchItem { SeasonStartYear = 2026, MatchDate = "12月12日", KickoffTime = kickoff };
        Assert.False(ScheduleViewModel.TryGetMatchStart(match, out _));
    }

    [Fact]
    public void Valid_kickoff_is_a_match_start()
    {
        var match = new MatchItem { SeasonStartYear = 2026, MatchDate = "12月12日", KickoffTime = " 13:05 " };
        Assert.True(ScheduleViewModel.TryGetMatchStart(match, out var start));
        Assert.Equal(new DateTime(2026, 12, 12, 13, 5, 0), start);
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
