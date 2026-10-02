using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class OfficialRankingUrlTests
{
    [Theory]
    [InlineData(2026, "https://league-one.jp/ranking/?year=2026")]
    [InlineData(2025, "https://league-one.jp/ranking/?year=2025")]
    public void Builds_official_ranking_url_for_season(int seasonStartYear, string expected)
    {
        Assert.Equal(expected, OfficialRankingUrl.ForSeason(seasonStartYear));
    }

    [Fact]
    public void Rejects_invalid_season_year()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OfficialRankingUrl.ForSeason(0));
    }
}
