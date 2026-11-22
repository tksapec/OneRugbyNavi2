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
    private static readonly Dictionary<string, string> KanaDigraphs = new(StringComparer.Ordinal)
    {
        ["きゃ"] = "kya", ["きゅ"] = "kyu", ["きょ"] = "kyo",
        ["ぎゃ"] = "gya", ["ぎゅ"] = "gyu", ["ぎょ"] = "gyo",
        ["しゃ"] = "sha", ["しゅ"] = "shu", ["しょ"] = "sho",
        ["じゃ"] = "ja", ["じゅ"] = "ju", ["じょ"] = "jo",
        ["ちゃ"] = "cha", ["ちゅ"] = "chu", ["ちょ"] = "cho",
        ["ぢゃ"] = "ja", ["ぢゅ"] = "ju", ["ぢょ"] = "jo",
        ["にゃ"] = "nya", ["にゅ"] = "nyu", ["にょ"] = "nyo",
        ["ひゃ"] = "hya", ["ひゅ"] = "hyu", ["ひょ"] = "hyo",
        ["びゃ"] = "bya", ["びゅ"] = "byu", ["びょ"] = "byo",
        ["ぴゃ"] = "pya", ["ぴゅ"] = "pyu", ["ぴょ"] = "pyo",
        ["みゃ"] = "mya", ["みゅ"] = "myu", ["みょ"] = "myo",
        ["りゃ"] = "rya", ["りゅ"] = "ryu", ["りょ"] = "ryo",
        ["てぃ"] = "ti", ["でぃ"] = "di", ["とぅ"] = "tu", ["どぅ"] = "du",
        ["ふぁ"] = "fa", ["ふぃ"] = "fi", ["ふぇ"] = "fe", ["ふぉ"] = "fo",
        ["うぃ"] = "wi", ["うぇ"] = "we", ["うぉ"] = "wo",
        ["しぇ"] = "she", ["ちぇ"] = "che", ["じぇ"] = "je"
    };

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

        var romanizedTokens = scope == PlayerSearchScope.All && tokens.Count <= 2 &&
                              tokens.All(token => token.Length >= 2 && token.All(IsKana))
            ? tokens.Select(ToHepburn).ToArray()
            : [];
        var normalizedKanaTokens = tokens.Select(NormalizeKana).ToArray();
        var expandedKanaTokens = tokens.Select(NormalizeKanaWithVowelExpansion).ToArray();

        return allPlayers
            .Where(player =>
            {
                var searchableFields = GetSearchFields(player, scope).Select(Normalize).Where(value => value.Length > 0).ToArray();
                return tokens.Select((token, index) => (token, index)).All(item =>
                    searchableFields.Any(field =>
                        field.Contains(item.token, StringComparison.Ordinal) ||
                        NormalizeKana(field).Contains(normalizedKanaTokens[item.index], StringComparison.Ordinal) ||
                        ((field.Contains('ー') || item.token.Contains('ー')) &&
                         NormalizeKanaWithVowelExpansion(field).Contains(expandedKanaTokens[item.index], StringComparison.Ordinal))) ||
                    (scope == PlayerSearchScope.All && MatchesSeason(player, item.token))) ||
                    (scope == PlayerSearchScope.All && MatchesJapaneseReading(player, romanizedTokens));
            })
            .ToArray();
    }

    private static bool MatchesJapaneseReading(PlayerRecord player, IReadOnlyList<string> romanizedTokens)
    {
        if (romanizedTokens.Count is < 1 or > 2 || !ContainsKanji(player.NameJa) || string.IsNullOrWhiteSpace(player.NameEn))
            return false;
        if (romanizedTokens.Any(token => token.Length < 2)) return false;

        var nameParts = player.NameEn.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizePublishedRomanization)
            .Where(value => value.Length > 0)
            .ToArray();
        if (nameParts.Length == 0) return false;

        if (romanizedTokens.Count == 1)
        {
            var query = romanizedTokens[0];
            if (nameParts.Any(part => part.Contains(query, StringComparison.Ordinal))) return true;
            if (nameParts.Length == 2)
            {
                var forward = nameParts[0] + nameParts[1];
                var reverse = nameParts[1] + nameParts[0];
                return forward.Contains(query, StringComparison.Ordinal) || reverse.Contains(query, StringComparison.Ordinal);
            }
            return false;
        }

        return nameParts.Length >= 2 && nameParts.Select((part, index) => (part, index)).Any(first =>
            first.part.Contains(romanizedTokens[0], StringComparison.Ordinal) &&
            nameParts.Where((_, index) => index != first.index)
                .Any(part => part.Contains(romanizedTokens[1], StringComparison.Ordinal)));
    }

    private static bool ContainsKanji(string value)
        => value.EnumerateRunes().Any(rune =>
            rune.Value is >= 0x3400 and <= 0x4DBF or
            >= 0x4E00 and <= 0x9FFF or
            >= 0xF900 and <= 0xFAFF or
            >= 0x20000 and <= 0x323AF);

    private static bool IsKana(char character)
        => character is >= '\u3040' and <= '\u309F' or >= '\u30A0' and <= '\u30FF';

    private static string ToHepburn(string value)
    {
        var hiragana = new StringBuilder(value.Length);
        foreach (var character in value)
            hiragana.Append(character is >= '\u30A1' and <= '\u30F6' ? (char)(character - 0x60) : character);

        var converted = hiragana.ToString();
        var romanized = new StringBuilder(converted.Length * 2);
        for (var index = 0; index < converted.Length; index++)
        {
            var character = converted[index];
            if (character == 'っ')
            {
                if (TryGetKanaRomanization(converted, index + 1, out var next, out _) && next.Length > 0)
                    romanized.Append(next.StartsWith("ch", StringComparison.Ordinal) ? 't' : next[0]);
                continue;
            }

            if (character == 'ー')
            {
                var vowel = LastVowel(romanized);
                if (vowel != '\0') romanized.Append(vowel);
                continue;
            }

            if (!TryGetKanaRomanization(converted, index, out var syllable, out var consumed))
                return "";
            romanized.Append(syllable);
            index += consumed - 1;
        }

        return NormalizeRomanization(romanized.ToString());
    }

    private static bool TryGetKanaRomanization(string value, int index, out string romanization, out int consumed)
    {
        romanization = "";
        consumed = 0;
        if ((uint)index >= (uint)value.Length) return false;

        if (index + 1 < value.Length && KanaDigraphs.TryGetValue(value.Substring(index, 2), out var digraph))
        {
            romanization = digraph;
            consumed = 2;
            return true;
        }

        romanization = value[index] switch
        {
            'あ' => "a", 'い' => "i", 'う' => "u", 'え' => "e", 'お' => "o",
            'ぁ' => "a", 'ぃ' => "i", 'ぅ' => "u", 'ぇ' => "e", 'ぉ' => "o",
            'か' => "ka", 'き' => "ki", 'く' => "ku", 'け' => "ke", 'こ' => "ko",
            'が' => "ga", 'ぎ' => "gi", 'ぐ' => "gu", 'げ' => "ge", 'ご' => "go",
            'さ' => "sa", 'し' => "shi", 'す' => "su", 'せ' => "se", 'そ' => "so",
            'ざ' => "za", 'じ' => "ji", 'ず' => "zu", 'ぜ' => "ze", 'ぞ' => "zo",
            'た' => "ta", 'ち' => "chi", 'つ' => "tsu", 'て' => "te", 'と' => "to",
            'だ' => "da", 'ぢ' => "ji", 'づ' => "zu", 'で' => "de", 'ど' => "do",
            'な' => "na", 'に' => "ni", 'ぬ' => "nu", 'ね' => "ne", 'の' => "no",
            'は' => "ha", 'ひ' => "hi", 'ふ' => "fu", 'へ' => "he", 'ほ' => "ho",
            'ば' => "ba", 'び' => "bi", 'ぶ' => "bu", 'べ' => "be", 'ぼ' => "bo",
            'ぱ' => "pa", 'ぴ' => "pi", 'ぷ' => "pu", 'ぺ' => "pe", 'ぽ' => "po",
            'ま' => "ma", 'み' => "mi", 'む' => "mu", 'め' => "me", 'も' => "mo",
            'や' => "ya", 'ゆ' => "yu", 'よ' => "yo",
            'ゃ' => "ya", 'ゅ' => "yu", 'ょ' => "yo", 'ゎ' => "wa",
            'ら' => "ra", 'り' => "ri", 'る' => "ru", 'れ' => "re", 'ろ' => "ro",
            'わ' => "wa", 'ゐ' => "wi", 'ゑ' => "we", 'を' => "o", 'ん' => "n",
            'ゔ' => "vu", 'ゕ' => "ka", 'ゖ' => "ke",
            _ => ""
        };
        consumed = romanization.Length > 0 ? 1 : 0;
        return consumed > 0;
    }

    private static char LastVowel(StringBuilder value)
    {
        for (var index = value.Length - 1; index >= 0; index--)
            if (value[index] is 'a' or 'e' or 'i' or 'o' or 'u') return value[index];
        return '\0';
    }

    private static string NormalizeRomanization(string value)
    {
        var normalized = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (character is < 'a' or > 'z') continue;
            if (normalized.Length > 0 && IsVowel(character) && normalized[^1] == character) continue;
            if (character == 'u' && normalized.Length > 0 && normalized[^1] == 'o') continue;
            normalized.Append(character);
        }
        return normalized.ToString();
    }

    private static string NormalizePublishedRomanization(string value)
        => NormalizeRomanization(value.Normalize(NormalizationForm.FormD));

    private static bool IsVowel(char character) => character is 'a' or 'e' or 'i' or 'o' or 'u';

    private static bool MatchesSeason(PlayerRecord player, string token)
    {
        if (token.Length != 6) return false;
        var year = token[..4];
        var suffix = token[4..];
        if (!int.TryParse(year, out var seasonYear) || !int.TryParse(suffix, out var seasonEnd)) return false;
        if (seasonEnd != (seasonYear + 1) % 100) return false;
        return (player.TeamHistory ?? []).Any(history => history is not null &&
            IsWithinRange(token, history.FromSeason, history.ToSeason));
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
            yield return player.CurrentTeamDisplayName;
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
        yield return player.CurrentTeamDisplayName;
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
            if (character == 'ー')
            {
                continue;
            }
            var hiragana = character is >= '\u30A1' and <= '\u30F6'
                ? (char)(character - 0x60)
                : character;
            builder.Append(hiragana switch
            {
                'ぁ' => 'あ', 'ぃ' => 'い', 'ぅ' => 'う', 'ぇ' => 'え', 'ぉ' => 'お',
                'ゃ' => 'や', 'ゅ' => 'ゆ', 'ょ' => 'よ', 'っ' => 'つ', 'ゎ' => 'わ', _ => hiragana
            });
        }

        return builder.ToString();
    }

    private static string NormalizeKanaWithVowelExpansion(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var hiragana = character is >= '\u30A1' and <= '\u30F6'
                ? (char)(character - 0x60)
                : character;
            var folded = hiragana switch
            {
                'ぁ' => 'あ', 'ぃ' => 'い', 'ぅ' => 'う', 'ぇ' => 'え', 'ぉ' => 'お',
                'ゃ' => 'や', 'ゅ' => 'ゆ', 'ょ' => 'よ', 'っ' => 'つ', 'ゎ' => 'わ', _ => hiragana
            };
            if (character == 'ー')
            {
                if (builder.Length > 0)
                {
                    var prolonged = KanaLongVowel(builder[^1]);
                    if (prolonged != '\0') builder.Append(prolonged);
                }
                continue;
            }
            builder.Append(folded);
        }
        return builder.ToString();
    }

    private static char KanaLongVowel(char previous) => previous switch
    {
        'あ' or 'か' or 'さ' or 'た' or 'な' or 'は' or 'ま' or 'や' or 'ら' or 'わ' or
            'が' or 'ざ' or 'だ' or 'ば' or 'ぱ' or 'ぁ' or 'ゃ' => 'あ',
        'い' or 'き' or 'し' or 'ち' or 'に' or 'ひ' or 'み' or 'り' or
            'ぎ' or 'じ' or 'ぢ' or 'び' or 'ぴ' or 'ゐ' or 'ぃ' => 'い',
        'う' or 'く' or 'す' or 'つ' or 'ぬ' or 'ふ' or 'む' or 'ゆ' or 'る' or
            'ぐ' or 'ず' or 'づ' or 'ぶ' or 'ぷ' or 'ゔ' or 'ぅ' or 'ゅ' => 'う',
        'え' or 'け' or 'せ' or 'て' or 'ね' or 'へ' or 'め' or 'れ' or
            'げ' or 'ぜ' or 'で' or 'べ' or 'ぺ' or 'ゑ' or 'ぇ' => 'い',
        'お' or 'こ' or 'そ' or 'と' or 'の' or 'ほ' or 'も' or 'よ' or 'ろ' or 'を' or
            'ご' or 'ぞ' or 'ど' or 'ぼ' or 'ぽ' or 'ぉ' => 'う',
        _ => '\0'
    };
}
