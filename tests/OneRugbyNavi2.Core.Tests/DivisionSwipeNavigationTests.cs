using OneRugbyNavi2;
using System.Xml.Linq;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class DivisionSwipeNavigationTests
{
    [Theory]
    [InlineData(1, DivisionSwipeDirection.Left, 2)]
    [InlineData(2, DivisionSwipeDirection.Left, 3)]
    [InlineData(2, DivisionSwipeDirection.Right, 1)]
    [InlineData(3, DivisionSwipeDirection.Right, 2)]
    public void Adjacent_division_is_returned_for_a_valid_horizontal_swipe(
        int currentDivision,
        DivisionSwipeDirection direction,
        int expectedDivision)
    {
        Assert.Equal(expectedDivision, DivisionSwipeNavigation.GetAdjacentDivision(currentDivision, direction));
    }

    [Theory]
    [InlineData(1, DivisionSwipeDirection.Right)]
    [InlineData(3, DivisionSwipeDirection.Left)]
    [InlineData(0, DivisionSwipeDirection.Left)]
    [InlineData(4, DivisionSwipeDirection.Right)]
    public void Invalid_division_or_swipe_at_boundary_does_not_navigate(
        int currentDivision,
        DivisionSwipeDirection direction)
    {
        Assert.Null(DivisionSwipeNavigation.GetAdjacentDivision(currentDivision, direction));
    }

    [Fact]
    public void Horizontal_division_swipes_are_not_attached_to_the_vertical_schedule_list()
    {
        XNamespace maui = "http://schemas.microsoft.com/dotnet/2021/maui";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2009/xaml";
        var page = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainPage.xaml"));
        var scheduleList = page.Descendants(maui + "CollectionView")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "list");
        var divisionTabs = page.Descendants(maui + "Grid")
            .Single(element => (string?)element.Attribute(xaml + "Name") == "divisionTabsGrid");

        Assert.Empty(scheduleList.Descendants(maui + "SwipeGestureRecognizer"));
        var tabSwipes = divisionTabs.Element(maui + "Grid.GestureRecognizers")?
            .Elements(maui + "SwipeGestureRecognizer").Count() ?? 0;
        Assert.Equal(2, tabSwipes);
    }
}
