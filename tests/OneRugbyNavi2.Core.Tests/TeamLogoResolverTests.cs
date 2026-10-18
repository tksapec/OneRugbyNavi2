using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class TeamLogoResolverTests
{
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
