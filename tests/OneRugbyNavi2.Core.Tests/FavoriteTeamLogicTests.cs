using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class FavoriteTeamLogicTests
{
    [Fact]
    public void InvalidV2MigratesLegacyFavorite()
    {
        var result = FavoriteTeamLogic.Load("not-json", "Legacy Club");

        Assert.Equal(new[] { "Legacy Club" }, result.Favorites);
        Assert.True(result.RequiresWriteBack);
    }

    [Fact]
    public void ValidV2DeduplicatesNames()
    {
        var result = FavoriteTeamLogic.Load("[\"Club\",\"Club\"]", "Legacy Club");

        Assert.Equal(new[] { "Club" }, result.Favorites);
        Assert.False(result.RequiresWriteBack);
    }

    [Fact]
    public void ToggleFavoriteUsesSeasonAwareIdentity()
    {
        var favorites = new[] { "埼玉ワイルドナイツ", "東芝ブレイブルーパス東京" };

        var updated = FavoriteTeamLogic.Toggle(favorites, "埼玉パナソニックワイルドナイツ", 2026);

        Assert.Equal(new[] { "東芝ブレイブルーパス東京" }, updated);
    }

    [Fact]
    public void FavoriteMatchUsesHistoricalAlias()
    {
        var match = new MatchItem
        {
            SeasonStartYear = 2026,
            HomeTeam = "リコーブラックラムズ東京",
            AwayTeam = "浦安D-Rocks"
        };

        Assert.True(FavoriteTeamLogic.MatchAnyFavorite(match, new[] { "ブラックラムズ東京" }));
    }

    [Fact]
    public void FindHighestFavoriteDivisionPrefersD1ThenD2ThenD3()
    {
        var d1 = new[] { Match("D1 favorite", "浦安D-Rocks", "横浜キヤノンイーグルス") };
        var d2 = new[] { Match("D2 favorite", "花園近鉄ライナーズ", "清水建設江東ブルーシャークス") };
        var d3 = new[] { Match("D3 favorite", "中国電力レッドレグリオンズ", "ヤクルトレビンズ戸田") };

        Assert.Equal(1, FavoriteTeamLogic.FindHighestFavoriteDivision(d1, d2, d3,
            new[] { "横浜キヤノンイーグルス", "花園近鉄ライナーズ", "ヤクルトレビンズ戸田" }));
        Assert.Equal(2, FavoriteTeamLogic.FindHighestFavoriteDivision(Array.Empty<MatchItem>(), d2, d3,
            new[] { "花園近鉄ライナーズ", "ヤクルトレビンズ戸田" }));
        Assert.Equal(3, FavoriteTeamLogic.FindHighestFavoriteDivision(Array.Empty<MatchItem>(), Array.Empty<MatchItem>(), d3,
            new[] { "ヤクルトレビンズ戸田" }));
    }

    [Fact]
    public void FindHighestFavoriteDivisionReturnsNullWhenUnmatched()
    {
        var d1 = new[] { Match("D1 unrelated", "浦安D-Rocks", "横浜キヤノンイーグルス") };

        Assert.Null(FavoriteTeamLogic.FindHighestFavoriteDivision(d1, Array.Empty<MatchItem>(), Array.Empty<MatchItem>(),
            new[] { "遠い架空クラブ" }));
    }

    [Fact]
    public void FavoriteFilterCombinesWithIndependentFilters()
    {
        var viewModel = NewViewModel();
        viewModel.SetItems(new[]
        {
            Fixture("favorite-tokyo", "浦安D-Rocks", "横浜キヤノンイーグルス", "東京", "秩父宮ラグビー場", daysFromToday: 3),
            Fixture("favorite-osaka", "花園近鉄ライナーズ", "横浜キヤノンイーグルス", "大阪", "花園ラグビー場", daysFromToday: 3),
            Fixture("unrelated-tokyo", "東京サントリーサンゴリアス", "埼玉パナソニックワイルドナイツ", "東京", "秩父宮ラグビー場", daysFromToday: 3),
            Fixture("favorite-past", "花園近鉄ライナーズ", "横浜キヤノンイーグルス", "東京", "秩父宮ラグビー場", daysFromToday: -3)
        }, Array.Empty<ScheduleFetcher.Item>(), Array.Empty<ScheduleFetcher.Item>());
        viewModel.FavoriteTeamFilters = new[] { "浦安D-Rocks", "花園近鉄ライナーズ" };
        viewModel.VenueFilter = "東京";
        viewModel.PeriodFilter = ScheduleViewModel.DateRangeFilter.Upcoming;

        viewModel.ApplyFilters();

        Assert.Equal(new[] { "favorite-tokyo" }, viewModel.FilteredItems.Select(match => match.MatchId));
    }

    [Fact]
    public void EmptyFavoriteFilterPreservesIndependentFilteredMatches()
    {
        var viewModel = NewViewModel();
        viewModel.SetItems(new[]
        {
            Fixture("favorite-tokyo", "浦安D-Rocks", "横浜キヤノンイーグルス", "東京", "秩父宮ラグビー場", daysFromToday: 3),
            Fixture("unrelated-tokyo", "東京サントリーサンゴリアス", "埼玉パナソニックワイルドナイツ", "東京", "秩父宮ラグビー場", daysFromToday: 3),
            Fixture("osaka", "花園近鉄ライナーズ", "横浜キヤノンイーグルス", "大阪", "花園ラグビー場", daysFromToday: 3)
        }, Array.Empty<ScheduleFetcher.Item>(), Array.Empty<ScheduleFetcher.Item>());
        viewModel.FavoriteTeamFilters = Array.Empty<string>();
        viewModel.VenueFilter = "東京";
        viewModel.PeriodFilter = ScheduleViewModel.DateRangeFilter.Upcoming;

        viewModel.ApplyFilters();

        Assert.Equal(new[] { "favorite-tokyo", "unrelated-tokyo" }, viewModel.FilteredItems.Select(match => match.MatchId));
    }

    private static MatchItem Match(string id, string home, string away) => new()
    {
        MatchId = id,
        SeasonStartYear = 2026,
        HomeTeam = home,
        AwayTeam = away
    };

    private static ScheduleViewModel NewViewModel() => new();

    private static ScheduleFetcher.Item Fixture(string id, string home, string away, string prefecture, string venue, int daysFromToday)
    {
        var date = DateTime.Today.AddDays(daysFromToday);
        return new ScheduleFetcher.Item
        {
            MatchId = id,
            SeasonStartYear = date.Month >= 9 ? date.Year : date.Year - 1,
            CategoryCode = "D1",
            Date = $"{date.Month}月{date.Day}日",
            Kickoff = "14:00",
            Home = home,
            Away = away,
            Pref = prefecture,
            Venue = venue
        };
    }
}
