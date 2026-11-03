namespace OneRugbyNavi2;

public enum BottomNavigationSection
{
    Schedule,
    Teams,
    Players,
    More
}

public static class BottomNavigationRoute
{
    public static string RouteFor(BottomNavigationSection section) => section switch
    {
        BottomNavigationSection.Schedule => "//SchedulePage",
        BottomNavigationSection.Teams => "TeamListPage",
        BottomNavigationSection.Players => "PlayersPage",
        BottomNavigationSection.More => "InfoPage",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
    };
}
