using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class AppInfoCopyTests
{
    [Fact]
    public void Data_source_notice_describes_online_fetch_and_saved_data_fallback()
    {
        Assert.Contains("日程・結果は公式サイトから取得", AppInfoCopy.DataSourceDescription);
        Assert.Contains("通信できない場合は前回保存したデータ", AppInfoCopy.DataSourceDescription);
        Assert.Contains("情報画面のDB件数は同梱データベース", AppInfoCopy.DataSourceDescription);
        Assert.DoesNotContain("ローカルDBを優先表示", AppInfoCopy.DataSourceDescription);
    }
}
