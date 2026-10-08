using System.Text;

namespace OneRugbyNavi2;

public static class PlayerSearch
{
    public static IReadOnlyList<PlayerRecord> Search(IEnumerable<PlayerRecord> players, string? query)
    {
        ArgumentNullException.ThrowIfNull(players);
        var allPlayers = players as IReadOnlyCollection<PlayerRecord> ?? players.ToArray();
        var tokens = SplitTokens(query);
        if (tokens.Count == 0)
        {
            return allPlayers.ToArray();
        }

        return allPlayers
            .Where(player =>
            {
                var searchableText = Normalize(string.Join(" ", GetSearchFields(player)));
                return tokens.All(token => searchableText.Contains(token, StringComparison.Ordinal));
            })
            .ToArray();
    }

    private static IEnumerable<string> GetSearchFields(PlayerRecord player)
    {
        yield return player.PlayerId;
        yield return player.NameJa;
        yield return player.NameEn;
        yield return player.CurrentTeamName;
        foreach (var value in player.Aliases) yield return value;
        foreach (var value in player.Positions) yield return value;
        foreach (var value in player.Schools) yield return value;
        foreach (var history in player.TeamHistory) yield return history.TeamName;
        foreach (var history in player.RepresentativeHistory)
        {
            yield return history.TeamName;
            yield return history.Level;
            foreach (var season in history.Seasons) yield return season;
        }
    }

    private static List<string> SplitTokens(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(token => token.Length > 0)
            .ToList();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
