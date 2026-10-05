using System.Text.Json;

namespace OneRugbyNavi2;

public sealed record FavoriteLoadResult(IReadOnlyList<string> Favorites, bool RequiresWriteBack);
public sealed record QuickFavoriteFilterResolution(int Division, IReadOnlyList<string> Favorites);

public static class FavoriteTeamLogic
{
    public static int? FindDivisionForTeam(
        string? team,
        int seasonStartYear,
        IReadOnlyList<MatchItem> div1,
        IReadOnlyList<MatchItem> div2,
        IReadOnlyList<MatchItem> div3)
    {
        if (string.IsNullOrWhiteSpace(team))
        {
            return null;
        }

        var divisions = new[] { div1, div2, div3 };
        for (var index = 0; index < divisions.Length; index++)
        {
            if (divisions[index].Any(match =>
                    SeasonCatalog.AreSameTeamName(match.HomeTeam, team, seasonStartYear) ||
                    SeasonCatalog.AreSameTeamName(match.AwayTeam, team, seasonStartYear)))
            {
                return index + 1;
            }
        }

        return null;
    }

    public static FavoriteLoadResult Load(string? json, string? legacyTeam)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var names = JsonSerializer.Deserialize<List<string>>(json);
                if (names is not null)
                {
                    return new FavoriteLoadResult(DeduplicateExact(names), false);
                }
            }
            catch (JsonException)
            {
                // An invalid v2 value is recovered from the legacy preference below.
            }
        }

        var legacy = string.IsNullOrWhiteSpace(legacyTeam)
            ? Array.Empty<string>()
            : new[] { legacyTeam.Trim() };
        return new FavoriteLoadResult(legacy, true);
    }

    public static string Serialize(IReadOnlyCollection<string> favorites)
        => JsonSerializer.Serialize(DeduplicateExact(favorites));

    public static string FormatFavoriteSummary(
        IReadOnlyList<string> favorites,
        string? selectedTeam,
        bool isSelectedFavorite)
    {
        var summary = favorites.Count == 0
            ? "お気に入り: 未登録"
            : $"登録済み ({favorites.Count}チーム):\n・" + string.Join("\n・", favorites);

        if (!string.IsNullOrWhiteSpace(selectedTeam))
        {
            summary += $"\n\n選択中: {selectedTeam}（{(isSelectedFavorite ? "登録済み" : "未登録")}）";
        }

        return summary;
    }

    public static IReadOnlyList<string> Toggle(IReadOnlyList<string> favorites, string team, int seasonStartYear)
    {
        var name = team?.Trim() ?? "";
        var current = favorites.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList();
        if (name.Length == 0)
        {
            return current;
        }

        if (current.Any(value => SeasonCatalog.AreSameTeamName(value, name, seasonStartYear)))
        {
            return current.Where(value => !SeasonCatalog.AreSameTeamName(value, name, seasonStartYear)).ToArray();
        }

        current.Add(name);
        return current;
    }

    public static IReadOnlyList<string> ToggleInDivision(
        IReadOnlyList<string> favorites,
        string team,
        int division,
        int seasonStartYear,
        IReadOnlyList<MatchItem> div1,
        IReadOnlyList<MatchItem> div2,
        IReadOnlyList<MatchItem> div3)
    {
        var name = team?.Trim() ?? "";
        var current = favorites
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        if (name.Length == 0 || division is < 1 or > 3)
        {
            return current;
        }

        var selectedIsFavorite = current.Any(value =>
            SeasonCatalog.AreSameTeamName(value, name, seasonStartYear));
        var divisionMatches = division switch
        {
            1 => div1,
            2 => div2,
            _ => div3
        };
        var currentDivisionFavorites = FindMatchingFavorites(divisionMatches, current);

        current.RemoveAll(value => currentDivisionFavorites.Any(existing =>
            SeasonCatalog.AreSameTeamName(value, existing, seasonStartYear)));
        if (!selectedIsFavorite)
        {
            current.Add(name);
        }

        var unique = new List<string>();
        foreach (var value in current.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!unique.Any(existing => SeasonCatalog.AreSameTeamName(existing, value, seasonStartYear)))
            {
                unique.Add(value);
            }
        }

        return unique;
    }

    public static IReadOnlyList<string> NormalizeByDivision(
        IReadOnlyList<string> favorites,
        int seasonStartYear,
        IReadOnlyList<MatchItem> div1,
        IReadOnlyList<MatchItem> div2,
        IReadOnlyList<MatchItem> div3)
    {
        var current = favorites
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        var divisions = new[] { div1, div2, div3 };
        var normalized = new List<string>();
        var assigned = new HashSet<string>(StringComparer.Ordinal);

        foreach (var matches in divisions)
        {
            var divisionFavorites = FindMatchingFavorites(matches, current);
            if (divisionFavorites.Count == 0)
            {
                continue;
            }

            normalized.Add(divisionFavorites[0]);
            foreach (var favorite in current)
            {
                if (divisionFavorites.Any(candidate =>
                    SeasonCatalog.AreSameTeamName(candidate, favorite, seasonStartYear)))
                {
                    assigned.Add(favorite);
                }
            }
        }

        normalized.AddRange(current.Where(favorite => !assigned.Contains(favorite)));

        var unique = new List<string>();
        foreach (var value in normalized)
        {
            if (!unique.Any(existing => SeasonCatalog.AreSameTeamName(existing, value, seasonStartYear)))
            {
                unique.Add(value);
            }
        }

        return unique;
    }

    public static bool MatchAnyFavorite(MatchItem match, IReadOnlyCollection<string> favorites)
    {
        if (favorites.Count == 0)
        {
            return false;
        }

        return favorites.Any(favorite =>
            SeasonCatalog.AreSameTeamName(match.HomeTeam, favorite, match.SeasonStartYear) ||
            SeasonCatalog.AreSameTeamName(match.AwayTeam, favorite, match.SeasonStartYear));
    }

    public static int? FindHighestFavoriteDivision(
        IReadOnlyList<MatchItem> div1,
        IReadOnlyList<MatchItem> div2,
        IReadOnlyList<MatchItem> div3,
        IReadOnlyList<string> favorites)
    {
        if (HasFavorite(div1, favorites)) return 1;
        if (HasFavorite(div2, favorites)) return 2;
        if (HasFavorite(div3, favorites)) return 3;
        return null;
    }

    public static QuickFavoriteFilterResolution? ResolveQuickFilter(
        int currentDivision,
        IReadOnlyList<MatchItem> div1,
        IReadOnlyList<MatchItem> div2,
        IReadOnlyList<MatchItem> div3,
        IReadOnlyList<string> favorites)
    {
        var divisions = new[] { div1, div2, div3 };
        if (currentDivision is >= 1 and <= 3)
        {
            var currentFavorites = FindMatchingFavorites(divisions[currentDivision - 1], favorites);
            if (currentFavorites.Count > 0)
            {
                return new QuickFavoriteFilterResolution(currentDivision, currentFavorites);
            }
        }

        var highestDivision = FindHighestFavoriteDivision(div1, div2, div3, favorites);
        if (highestDivision is null)
        {
            return null;
        }

        return new QuickFavoriteFilterResolution(highestDivision.Value,
            FindMatchingFavorites(divisions[highestDivision.Value - 1], favorites));
    }

    public static IReadOnlyList<string> FindMatchingFavorites(
        IReadOnlyList<MatchItem> matches,
        IReadOnlyList<string> favorites)
        => favorites
            .Where(favorite => matches.Any(match => MatchAnyFavorite(match, new[] { favorite })))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool HasFavorite(IReadOnlyList<MatchItem> matches, IReadOnlyCollection<string> favorites)
        => matches.Any(match => MatchAnyFavorite(match, favorites));

    private static IReadOnlyList<string> DeduplicateExact(IEnumerable<string?> names)
        => names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
