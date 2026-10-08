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
                var searchableFields = GetSearchFields(player).Select(Normalize).Where(value => value.Length > 0).ToArray();
                return tokens.All(token => searchableFields.Any(field =>
                    field.Contains(token, StringComparison.Ordinal) ||
                    NormalizeKana(field).Contains(NormalizeKana(token), StringComparison.Ordinal)));
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
