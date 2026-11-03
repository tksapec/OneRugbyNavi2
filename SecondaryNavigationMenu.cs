namespace OneRugbyNavi2;

public static class SecondaryNavigationMenu
{
    private static readonly string[] StandardItems = { "順位表", "ランキング", "アプリ情報" };

    public static string[] BuildItems(IEnumerable<string>? additionalItems = null)
    {
        return (additionalItems ?? Enumerable.Empty<string>())
            .Concat(StandardItems)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
