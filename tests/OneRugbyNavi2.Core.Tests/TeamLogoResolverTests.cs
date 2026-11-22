using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class TeamLogoResolverTests
{
    [Theory]
    [InlineData("埼玉ワイルドナイツ", "saitama_panasonic_wild_knights_2026.png")]
    [InlineData("東京サンゴリアス", "tokyo_suntory_sungoliath_2026.png")]
    [InlineData("ブラックラムズ東京", "ricoh_blackrams_tokyo_2026.png")]
    [InlineData("スカイアクティブズ広島", "mazda_skyactivs_hiroshima_2026.png")]
    [InlineData("グリーンウォリアーズ東葛", "jr_east_green_warriors_tokatsu_2026.png")]
    [InlineData("NECグリーンロケッツ東葛", "jr_east_green_warriors_tokatsu_2026.png")]
    [InlineData("AZ-COM丸和MOMOTARO'S", "maruwa_momotaros_narita_2026.png")]
    [InlineData("ヤマハ静岡ブルーレヴズ", "shizuoka_blue_revs.png")]
    public void Current_schedule_aliases_use_the_current_bundled_logo(string name, string expected)
    {
        Assert.Equal(expected, TeamLogoResolver.GetLogoPath(name, seasonStartYear: 2026));
    }

    [Fact]
    public void Historical_green_rockets_keep_the_historical_logo()
    {
        Assert.Equal("green_rockets_tokatsu.png", TeamLogoResolver.GetLogoPath("NECグリーンロケッツ東葛", seasonStartYear: 2025));
    }

    [Fact]
    public void Shizuoka_rename_keeps_the_same_current_team_identity()
    {
        Assert.Equal("124", SeasonCatalog.GetCurrentTeam("ヤマハ静岡ブルーレヴズ")?.LeagueOneTeamId);
        Assert.True(SeasonCatalog.AreSameTeamName("静岡ブルーレヴズ", "ヤマハ静岡ブルーレヴズ", 2026));
        Assert.Equal("ヤマハ静岡ブルーレヴズ", SeasonCatalog.GetTeamDisplayName("静岡ブルーレヴズ", 2026));
        Assert.Equal("静岡ブルーレヴズ", SeasonCatalog.GetTeamDisplayName("静岡ブルーレヴズ", 2025));
        Assert.Equal("shizuoka_blue_revs.png", TeamLogoResolver.GetLogoPath("静岡ブルーレヴズ", seasonStartYear: 2025));
    }

    [Fact]
    public void Cached_player_team_can_be_searched_under_both_names()
    {
        var player = new PlayerRecord { CurrentTeamId = "124", CurrentTeamName = "静岡ブルーレヴズ" };

        Assert.Equal("ヤマハ静岡ブルーレヴズ", player.CurrentTeamDisplayName);
        Assert.Equal("静岡ブルーレヴズ", player.CurrentTeamName);
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "ヤマハ静岡ブルーレヴズ", PlayerSearchScope.CurrentTeam)));
        Assert.Same(player, Assert.Single(PlayerSearch.Search([player], "静岡ブルーレヴズ", PlayerSearchScope.CurrentTeam)));
    }

    [Fact]
    public void Every_current_team_has_a_bundled_logo_when_device_cache_is_empty()
    {
        var missing = SeasonCatalog.Teams2026
            .Where(team => string.IsNullOrWhiteSpace(TeamLogoResolver.GetLogoPath(team.TeamName, seasonStartYear: 2026)))
            .Select(team => team.TeamName)
            .ToArray();

        Assert.True(missing.Length == 0, $"No bundled logo fallback for: {string.Join(", ", missing)}.");

        foreach (var team in SeasonCatalog.Teams2026)
        {
            var logoFileName = TeamLogoResolver.GetLogoPath(team.TeamName, seasonStartYear: 2026)!;
            var packagedFixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Teams", logoFileName);
            Assert.True(File.Exists(packagedFixture), $"Missing packaged logo asset for {team.TeamName}: {logoFileName}.");
        }
    }
}
