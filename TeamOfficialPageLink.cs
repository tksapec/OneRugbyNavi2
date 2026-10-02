namespace OneRugbyNavi2;

public static class TeamOfficialPageLink
{
    public static bool TryFindTeamUrl(IEnumerable<TeamIndexEntry> teams, string? teamName, int seasonStartYear, out string teamUrl)
    {
        teamUrl = "";
        if (string.IsNullOrWhiteSpace(teamName)) return false;

        var team = teams.FirstOrDefault(candidate =>
            SeasonCatalog.AreSameTeamName(candidate.TeamName, teamName, seasonStartYear));
        if (team is null || !TryCreateUri(team.TeamUrl, out _)) return false;

        teamUrl = team.TeamUrl;
        return true;
    }

    public static bool TryCreateUri(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            !parsed.IsDefaultPort ||
            !parsed.Host.Equals("league-one.jp", StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(parsed.AbsolutePath, @"^/team/\d+/?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
