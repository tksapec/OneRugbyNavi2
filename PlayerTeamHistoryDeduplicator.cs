using System.Globalization;
using System.Text;

namespace OneRugbyNavi2;

public static class PlayerTeamHistoryDeduplicator
{
    private sealed record DatedHistory(PlayerTeamHistory History, int Start, int End, bool HasKnownStart);

    public static IReadOnlyList<PlayerTeamHistory> Normalize(PlayerRecord player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var result = new List<PlayerTeamHistory>();
        var histories = (player.TeamHistory ?? []).Where(history => history is not null).ToArray();

        foreach (var typeGroup in histories.GroupBy(history => history.Type, StringComparer.Ordinal))
        foreach (var teamGroup in typeGroup.GroupBy(history => NormalizeTeamName(history.TeamName), StringComparer.Ordinal))
        {
            var undated = new List<PlayerTeamHistory>();
            var dated = new List<DatedHistory>();
            foreach (var history in teamGroup)
            {
                if (!TryGetRange(history, out var start, out var end, out var hasKnownStart))
                    undated.Add(history);
                else
                    dated.Add(new DatedHistory(history, start, end, hasKnownStart));
            }

            dated.Sort((left, right) => left.Start != right.Start
                ? left.Start.CompareTo(right.Start)
                : left.End.CompareTo(right.End));

            var index = 0;
            while (index < dated.Count)
            {
                var first = dated[index++];
                var start = first.Start;
                var end = first.End;
                var hasKnownStart = first.HasKnownStart;
                var fromSeason = first.HasKnownStart ? first.History.FromSeason : "";
                var teamId = first.History.TeamId;
                var teamName = first.History.TeamName;

                while (index < dated.Count && CanMerge(dated[index], end, hasKnownStart))
                {
                    var next = dated[index++];
                    start = Math.Min(start, next.Start);
                    end = Math.Max(end, next.End);
                    if (next.HasKnownStart && (!hasKnownStart || next.Start < SeasonStartYear(fromSeason)))
                        fromSeason = next.History.FromSeason;
                    hasKnownStart |= next.HasKnownStart;
                    if (IsCurrentTeam(player, next.History))
                    {
                        teamId = player.CurrentTeamId;
                        teamName = player.CurrentTeamName;
                    }
                }

                if (IsCurrentTeam(player, first.History))
                {
                    teamId = player.CurrentTeamId;
                    teamName = player.CurrentTeamName;
                }
                result.Add(new PlayerTeamHistory
                {
                    TeamId = teamId,
                    TeamName = teamName,
                    Type = first.History.Type,
                    FromSeason = fromSeason,
                    ToSeason = end == int.MaxValue ? "" : ToSeason(end)
                });
            }

            if (dated.Count == 0 && undated.Count > 0)
            {
                var preferred = undated.FirstOrDefault(history => IsCurrentTeam(player, history)) ?? undated[0];
                result.Add(preferred);
            }
        }

        return result
            .OrderBy(history => HistoryTypeOrder(history.Type))
            .ThenBy(history => FirstKnownYear(history))
            .ThenBy(history => SeasonStartYear(history.FromSeason))
            .ThenBy(history => history.TeamName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CanMerge(DatedHistory next, int currentEnd, bool currentHasKnownStart)
    {
        if (next.Start <= currentEnd) return true;
        // Do not turn two end-only records from separate seasons into a made-up continuous stay.
        if (!currentHasKnownStart || !next.HasKnownStart) return false;
        return next.Start <= (currentEnd == int.MaxValue ? int.MaxValue : currentEnd + 1);
    }

    private static bool IsCurrentTeam(PlayerRecord player, PlayerTeamHistory history)
        => string.Equals(history.TeamId, player.CurrentTeamId, StringComparison.Ordinal) ||
           string.Equals(NormalizeTeamName(history.TeamName), NormalizeTeamName(player.CurrentTeamName), StringComparison.Ordinal);

    private static string NormalizeTeamName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool TryGetRange(PlayerTeamHistory history, out int start, out int end, out bool hasKnownStart)
    {
        hasKnownStart = TrySeasonStartYear(history.FromSeason, out start);
        var hasEnd = TrySeasonStartYear(history.ToSeason, out end);
        if (!hasKnownStart && !hasEnd)
        {
            start = 0;
            end = 0;
            return false;
        }

        // An end-only record identifies that season, not an inferred earlier start date.
        if (!hasKnownStart) start = end;
        if (!hasEnd) end = int.MaxValue;
        return true;
    }

    private static bool TrySeasonStartYear(string? season, out int year)
    {
        year = 0;
        return !string.IsNullOrWhiteSpace(season) && int.TryParse(season.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out year);
    }

    private static int FirstKnownYear(PlayerTeamHistory history)
        => TrySeasonStartYear(history.FromSeason, out var from) ? from :
            TrySeasonStartYear(history.ToSeason, out var to) ? to : int.MaxValue;

    private static int SeasonStartYear(string? season) => TrySeasonStartYear(season, out var year) ? year : int.MaxValue;
    private static int HistoryTypeOrder(string? type) => type switch { "highSchool" => 0, "university" => 1, "club" => 2, _ => 3 };
    private static string ToSeason(int year) => $"{year}-{(year + 1) % 100:00}";
}
