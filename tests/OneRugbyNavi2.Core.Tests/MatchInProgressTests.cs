using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class MatchInProgressTests
{
    [Theory]
    [InlineData("試合中")]
    [InlineData("開催中")]
    [InlineData("LIVE")]
    [InlineData("In Progress")]
    public void Recognizes_explicit_in_progress_status(string status)
    {
        var match = new MatchItem { MatchStatus = status };

        Assert.True(match.IsInProgress);
        Assert.True(match.HasSpecialStatus);
    }

    [Fact]
    public void Explicit_live_status_takes_precedence_over_score_presence()
    {
        var match = new MatchItem { MatchStatus = "LIVE", HomeScore = 12, AwayScore = 10 };

        Assert.True(match.IsInProgress);
        Assert.False(match.IsCompleted);
    }
}
