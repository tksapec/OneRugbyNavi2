using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class BottomNavigationRouteTests
{
    [Theory]
    [InlineData(BottomNavigationSection.Schedule, "//SchedulePage")]
    [InlineData(BottomNavigationSection.Teams, "TeamListPage")]
    [InlineData(BottomNavigationSection.Players, "PlayersPage")]
    [InlineData(BottomNavigationSection.More, "InfoPage")]
    public void RouteForReturnsSharedShellDestination(BottomNavigationSection section, string expected)
    {
        Assert.Equal(expected, BottomNavigationRoute.RouteFor(section));
    }
}
