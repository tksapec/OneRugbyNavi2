using Xunit;
using System.ComponentModel;

namespace OneRugbyNavi2.Core.Tests;

public class MatchScoreDisplayTests
{
    [Theory]
    [InlineData(24, 17, "24", "17")]
    [InlineData(0, 0, "0", "0")]
    [InlineData(null, null, "-", "-")]
    [InlineData(24, null, "-", "-")]
    [InlineData(null, 17, "-", "-")]
    public void SeparateRowsShowScoresOnlyWhenBothAreKnown(int? home, int? away, string expectedHome, string expectedAway)
    {
        var match = new MatchItem { HomeScore = home, AwayScore = away };

        Assert.Equal(expectedHome, match.HomeScoreText);
        Assert.Equal(expectedAway, match.AwayScoreText);
    }

    [Fact]
    public void PostponedMatchKeepsItsStatusWithoutInventingScores()
    {
        var match = new MatchItem { MatchStatus = "延期" };

        Assert.Equal("-", match.HomeScoreText);
        Assert.Equal("-", match.AwayScoreText);
        Assert.True(match.HasSpecialStatus);
        Assert.Equal("延期", match.StatusBadgeText);
        Assert.Equal("延期", match.ScoreText);
    }

    [Fact]
    public void ResultVisibilityCanBeToggledAndNotifiesScoreBindings()
    {
        var match = new MatchItem { HomeScore = 24, AwayScore = 17 };
        var changed = new List<string?>();
        match.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        match.AreScoresHidden = true;

        Assert.Equal("••", match.HomeScoreText);
        Assert.Equal("••", match.AwayScoreText);
        Assert.Equal("結果を表示", match.ResultVisibilityActionText);
        Assert.Contains(nameof(MatchItem.HomeScoreText), changed);
        Assert.Contains(nameof(MatchItem.AwayScoreText), changed);

        match.AreScoresHidden = false;
        Assert.Equal("24", match.HomeScoreText);
        Assert.Equal("17", match.AwayScoreText);
    }

    [Fact]
    public void ResultVisibilityCannotHideAnUnknownScore()
    {
        var match = new MatchItem { HomeScore = 24 };

        match.AreScoresHidden = true;

        Assert.False(match.AreScoresHidden);
        Assert.Equal("-", match.HomeScoreText);
        Assert.Equal("-", match.AwayScoreText);
    }
}
