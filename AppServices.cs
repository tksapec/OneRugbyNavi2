namespace OneRugbyNavi2;

public static class AppServices
{
    public static LeagueOneDatabase Database { get; } = new();

    public static TeamUpdateService TeamUpdater { get; } = new(Database);
}
