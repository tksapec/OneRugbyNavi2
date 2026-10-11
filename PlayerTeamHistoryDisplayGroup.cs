using System.Globalization;
using System.Text;

namespace OneRugbyNavi2;

public sealed class PlayerTeamHistoryDisplayGroup
{
    private PlayerTeamHistoryDisplayGroup(string teamName, string type, IReadOnlyList<PlayerTeamHistory> stints)
    {
        TeamName = teamName;
        Type = type;
        Stints = stints;
        Periods = string.Join(" / ", stints.Select(FormatPeriod));
        FirstSeasonYear = stints.Select(FirstKnownYear).DefaultIfEmpty(int.MaxValue).Min();
    }

    public string TeamName { get; }
    public string Type { get; }
    public IReadOnlyList<PlayerTeamHistory> Stints { get; }
    public string Periods { get; }
    private int FirstSeasonYear { get; }

    internal static bool SameInstitutionName(string? left, string? right)
        => string.Equals(NormalizeTeamName(left), NormalizeTeamName(right), StringComparison.Ordinal);

    public static IReadOnlyList<PlayerTeamHistoryDisplayGroup> Create(PlayerRecord player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return PlayerTeamHistoryDeduplicator.Normalize(player)
            .GroupBy(history => (history.Type, Team: NormalizeTeamName(history.TeamName)))
            .Select(group => new PlayerTeamHistoryDisplayGroup(
                group.First().TeamName,
                group.Key.Type,
                group.OrderBy(FirstKnownYear).ThenBy(history => history.FromSeason, StringComparer.Ordinal).ToArray()))
            .OrderBy(group => HistoryTypeOrder(group.Type))
            .ThenBy(group => group.FirstSeasonYear)
            .ThenBy(group => group.TeamName, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeTeamName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static int FirstKnownYear(PlayerTeamHistory history)
    {
        var season = string.IsNullOrWhiteSpace(history.FromSeason) ? history.ToSeason : history.FromSeason;
        return !string.IsNullOrWhiteSpace(season) && int.TryParse(season.AsSpan(0, Math.Min(4, season.Length)),
            NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? year : int.MaxValue;
    }

    private static string FormatPeriod(PlayerTeamHistory history)
    {
        if (string.IsNullOrWhiteSpace(history.FromSeason))
            return string.IsNullOrWhiteSpace(history.ToSeason) ? "時期未確認" : $"〜{history.ToSeason}";
        if (string.IsNullOrWhiteSpace(history.ToSeason)) return $"{history.FromSeason}〜現在";
        return history.FromSeason == history.ToSeason ? history.FromSeason : $"{history.FromSeason}〜{history.ToSeason}";
    }

    private static int HistoryTypeOrder(string? type) => type switch
    {
        "highSchool" => 0,
        "university" => 1,
        "club" => 2,
        _ => 3
    };
}
