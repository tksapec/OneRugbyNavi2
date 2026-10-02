using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class TeamOfficialPageLinkTests
{
    [Theory]
    [InlineData("https://league-one.jp/team/123", "https://league-one.jp/team/123")]
    [InlineData("https://league-one.jp/team/123/", "https://league-one.jp/team/123")]
    public void Accepts_absolute_https_official_team_urls(string source, string expected)
    {
        Assert.True(TeamOfficialPageLink.TryCreateUri(source, out var uri));
        Assert.Equal(expected, uri.AbsoluteUri.TrimEnd('/'));
    }

    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///tmp/team")]
    [InlineData("//league-one.jp/team/123")]
    [InlineData("http://league-one.jp/team/123")]
    [InlineData("https://example.com/team/123")]
    [InlineData("https://league-one.jp/other/123")]
    public void Rejects_empty_relative_or_non_http_team_urls(string source)
    {
        Assert.False(TeamOfficialPageLink.TryCreateUri(source, out _));
    }

    [Fact]
    public void Finds_official_page_for_a_2026_team_name_alias()
    {
        var teams = new[]
        {
            new TeamIndexEntry("131", "栃木ホンダヒート", "DIV1", "https://league-one.jp/team/131", "")
        };

        Assert.True(TeamOfficialPageLink.TryFindTeamUrl(teams, "三重ホンダヒート", 2026, out var teamUrl));
        Assert.Equal("https://league-one.jp/team/131", teamUrl);
    }
}
