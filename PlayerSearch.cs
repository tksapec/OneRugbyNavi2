using System.Text;

namespace OneRugbyNavi2;

public enum PlayerSearchScope
{
    All,
    CurrentTeam,
    PreviousTeam,
    School
}

public static class PlayerSearch
{
    public static IReadOnlyList<PlayerRecord> Search(
        IEnumerable<PlayerRecord> players,
        string? query,
        PlayerSearchScope scope = PlayerSearchScope.All)
    {
        ArgumentNullException.ThrowIfNull(players);
        var allPlayers = players.Where(player => player is not null).ToArray();
        var tokens = SplitTokens(query);
        if (tokens.Count == 0)
        {
            return allPlayers.ToArray();
        }

        return allPlayers
            .Where(player =>
            {
                var searchableFields = GetSearchFields(player, scope).Select(Normalize).Where(value => value.Length > 0).ToArray();
                return tokens.All(token => searchableFields.Any(field =>
                    field.Contains(token, StringComparison.Ordinal) ||
                    NormalizeKana(field).Contains(NormalizeKana(token), StringComparison.Ordinal))) ||
                    (scope == PlayerSearchScope.All && MatchesSeason(player, tokens));
            })
            .ToArray();
    }

    private static bool MatchesSeason(PlayerRecord player, IReadOnlyList<string> tokens)
    {
        if (tokens.Count != 1 || tokens[0].Length != 6) return false;
        var year = tokens[0][..4];
        var suffix = tokens[0][4..];
        if (!int.TryParse(year, out var seasonYear) || !int.TryParse(suffix, out var seasonEnd)) return false;
        if (seasonEnd != (seasonYear + 1) % 100) return false;
        return (player.TeamHistory ?? []).Any(history => history is not null &&
            IsWithinRange(tokens[0], history.FromSeason, history.ToSeason));
    }

    private static bool IsWithinRange(string season, string? from, string? to)
    {
        if (!IsNormalizedSeason(season)) return false;
        if (!IsSeason(from)) return IsSeason(to) && string.Equals(season, to, StringComparison.Ordinal);
        var year = int.Parse(season[..4]);
        var first = int.Parse(from![..4]);
        if (!IsSeason(to)) return year >= first;
        return year >= first && year <= int.Parse(to![..4]);
    }

    private static bool IsNormalizedSeason(string? value)
        => value is { Length: 6 } && int.TryParse(value[..4], out var year) &&
           int.TryParse(value[4..], out var end) && end == (year + 1) % 100;

    private static bool IsSeason(string? value)
        => value is { Length: 7 } && value[4] == '-' && int.TryParse(value[..4], out var year) &&
           int.TryParse(value[5..], out var end) && end == (year + 1) % 100;

    private static IEnumerable<string?> GetSearchFields(PlayerRecord player, PlayerSearchScope scope)
    {
        if (scope == PlayerSearchScope.CurrentTeam)
        {
            yield return player.CurrentTeamName;
            yield break;
        }

        if (scope == PlayerSearchScope.PreviousTeam)
        {
            foreach (var history in (player.TeamHistory ?? []).Where(history => history is not null &&
                         (history.TeamId != player.CurrentTeamId || history.TeamName != player.CurrentTeamName)))
                yield return history.TeamName;
            yield break;
        }

        if (scope == PlayerSearchScope.School)
        {
            foreach (var school in player.Schools ?? []) yield return school;
            yield break;
        }

        yield return player.PlayerId;
        yield return player.NameJa;
        yield return player.NameEn;
        yield return player.CurrentTeamName;
        foreach (var value in player.Aliases ?? []) yield return value;
        foreach (var value in player.Positions ?? []) yield return value;
        foreach (var nationality in player.Nationalities ?? []) yield return nationality;
        foreach (var category in player.RegistrationCategories ?? [])
        {
            if (category is null) continue;
            yield return category.Season;
            yield return category.Category;
        }
        foreach (var value in player.Schools ?? []) yield return value;
        foreach (var history in player.TeamHistory ?? [])
        {
            if (history is null) continue;
            yield return history.TeamName;
            if (!IsSeason(history.FromSeason) && IsSeason(history.ToSeason)) yield return history.ToSeason;
            foreach (var season in EnumerateSeasons(history.FromSeason, history.ToSeason)) yield return season;
        }
        foreach (var history in player.RepresentativeHistory ?? [])
        {
            if (history is null) continue;
            yield return history.TeamName;
            yield return history.Level;
            foreach (var season in history.Seasons ?? []) yield return season;
        }
    }

    private static IEnumerable<string> EnumerateSeasons(string? from, string? to)
    {
        if (!IsSeason(from)) yield break;
        var first = int.Parse(from![..4]);
        var last = IsSeason(to) ? int.Parse(to![..4]) : first;
        if (last - first > 80) yield break;
        for (var year = first; year <= last; year++) yield return $"{year}-{(year + 1) % 100:00}";
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

    private static string NormalizeKana(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= '\u30A1' and <= '\u30F6')
            {
                var hiragana = (char)(character - 0x60);
                builder.Append(hiragana switch
                {
                    'ぁ' => 'あ', 'ぃ' => 'い', 'ぅ' => 'う', 'ぇ' => 'え', 'ぉ' => 'お',
                    'ゃ' => 'や', 'ゅ' => 'ゆ', 'ょ' => 'よ', 'っ' => 'つ', 'ゎ' => 'わ', _ => hiragana
                });
            }
            else if (character == 'ー')
            {
                continue;
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
