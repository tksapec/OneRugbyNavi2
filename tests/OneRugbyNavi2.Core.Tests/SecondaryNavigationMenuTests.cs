using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class SecondaryNavigationMenuTests
{
    [Fact]
    public void StandardMenuContainsOnlySecondaryDestinations()
    {
        var items = SecondaryNavigationMenu.BuildItems();

        Assert.Equal(new[] { "順位表", "ランキング", "アプリ情報" }, items);
        Assert.DoesNotContain("日程・結果", items);
        Assert.DoesNotContain("チーム一覧", items);
        Assert.DoesNotContain("選手検索", items);
    }

    [Fact]
    public void AdditionalActionsAppearBeforeStandardItemsWithoutDuplicates()
    {
        var items = SecondaryNavigationMenu.BuildItems(new[] { "更新状況", "キャッシュ削除", "ランキング" });

        Assert.Equal(new[] { "更新状況", "キャッシュ削除", "ランキング", "順位表", "アプリ情報" }, items);
        Assert.Equal(items.Length, items.Distinct(StringComparer.Ordinal).Count());
    }
}
