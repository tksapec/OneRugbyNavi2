using System.Text;
using System.Text.RegularExpressions;

namespace OneRugbyNavi2;

public sealed record PlayerDuplicateCandidate(
    PlayerRecord First,
    PlayerRecord Second,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Finds records that need a human identity check. It never merges or edits records.
/// </summary>
public static partial class PlayerCatalogDuplicateDetector
{
    [GeneratedRegex(@"(?<![A-Z0-9])(?:NO\.?8|PR|HO|LO|FL|SH|SO|CTB|WTB|FB|UTB|UBK)(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PositionCodeRegex();

    public static IReadOnlyList<PlayerDuplicateCandidate> FindCandidates(IEnumerable<PlayerRecord> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var records = players.Where(player => player is not null).ToArray();
        var reasons = new Dictionary<(int First, int Second), HashSet<string>>();

        void Add(int firstIndex, int secondIndex, string reason)
        {
            var key = firstIndex < secondIndex ? (firstIndex, secondIndex) : (secondIndex, firstIndex);
            if (!reasons.TryGetValue(key, out var found)) reasons[key] = found = new HashSet<string>(StringComparer.Ordinal);
            found.Add(reason);
        }

        var officialIds = records
            .Select((player, index) => (player, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.player.LeagueOnePlayerId))
            .GroupBy(item => item.player.LeagueOnePlayerId.Trim(), StringComparer.Ordinal);
        foreach (var group in officialIds)
        {
            var matches = group.ToArray();
            for (var i = 0; i < matches.Length; i++)
            for (var j = i + 1; j < matches.Length; j++)
                Add(matches[i].index, matches[j].index, "同じリーグワン公式選手ID");
        }

        var byTeam = records
            .Select((player, index) => (player, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.player.CurrentTeamId))
            .GroupBy(item => item.player.CurrentTeamId, StringComparer.Ordinal);
        foreach (var team in byTeam)
        {
            var members = team.ToArray();
            for (var i = 0; i < members.Length; i++)
            for (var j = i + 1; j < members.Length; j++)
            {
                var first = members[i].player;
                var second = members[j].player;
                var firstNames = IdentityNames(first);
                if (firstNames.Overlaps(IdentityNames(second)))
                    Add(members[i].index, members[j].index, "同一チーム内の正規化氏名・別表記一致");

                if (first.BirthDate is null || first.BirthDate != second.BirthDate) continue;
                var matchingFacts = 0;
                if (first.HeightCm is { } height && second.HeightCm == height) matchingFacts++;
                if (first.WeightKg is { } weight && second.WeightKg == weight) matchingFacts++;
                if (PositionCodes(first).Overlaps(PositionCodes(second))) matchingFacts++;
                if (matchingFacts >= 2)
                    Add(members[i].index, members[j].index, "生年月日と身長・体重・ポジションの複数一致");
            }
        }

        return reasons
            .OrderBy(pair => pair.Key.First)
            .ThenBy(pair => pair.Key.Second)
            .Select(pair => new PlayerDuplicateCandidate(
                records[pair.Key.First],
                records[pair.Key.Second],
                pair.Value.Order(StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static HashSet<string> IdentityNames(PlayerRecord player)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        AddName(player.NameJa);
        AddName(player.NameEn);
        foreach (var alias in player.Aliases ?? []) AddName(alias);
        return names;

        void AddName(string? value)
        {
            var normalized = NormalizeName(value);
            if (normalized.Length > 0) names.Add(normalized);
        }
    }

    private static HashSet<string> PositionCodes(PlayerRecord player)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in player.Positions ?? [])
        {
            if (string.IsNullOrWhiteSpace(position)) continue;
            foreach (Match match in PositionCodeRegex().Matches(position.Normalize(NormalizationForm.FormKC)))
            {
                var code = match.Value.Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();
                if (code == "UBK") code = "UTB";
                codes.Add(code);
            }
        }

        return codes;
    }
}
