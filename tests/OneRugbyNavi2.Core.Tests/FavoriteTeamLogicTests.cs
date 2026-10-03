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

    private static MatchItem Match(string id, string home, string away) => new()
    {
        MatchId = id,
        SeasonStartYear = 2026,
        HomeTeam = home,
        AwayTeam = away
    };
}
