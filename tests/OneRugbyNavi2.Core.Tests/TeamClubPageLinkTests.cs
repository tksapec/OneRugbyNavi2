using OneRugbyNavi2;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class TeamClubPageLinkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Official_page_card_is_not_interactive_when_no_url_was_fetched(string? source)
    {
        Assert.False(TeamClubPageLink.CanOpenOfficialPage(source));
    }

    [Fact]
    public void Official_page_card_is_interactive_when_a_valid_club_url_was_fetched()
    {
        Assert.True(TeamClubPageLink.CanOpenOfficialPage("https://urayasu-d-rocks.com/"));
    }

    [Theory]
    [InlineData("https://urayasu-d-rocks.com/", "https://urayasu-d-rocks.com/")]
    [InlineData("https://club.example.jp/team/2026?from=league-one", "https://club.example.jp/team/2026?from=league-one")]
    public void Accepts_https_team_page_urls_extracted_from_the_league_index(string source, string expected)
    {
        Assert.True(TeamClubPageLink.TryCreateUri(source, out var uri));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://club.example.jp/")]
    [InlineData("https://league-one.jp/team/126")]
    [InlineData("https://user:password@club.example.jp/")]
    [InlineData("https://club.example.jp:8443/")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_non_https_or_league_one_page_urls(string source)
    {
        Assert.False(TeamClubPageLink.TryCreateUri(source, out _));
    }
}
