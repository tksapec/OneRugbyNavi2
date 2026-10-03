using System.Text.Json;

namespace OneRugbyNavi2;

public sealed record FavoriteLoadResult(IReadOnlyList<string> Favorites, bool RequiresWriteBack);

public static class FavoriteTeamLogic
{
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

    private static bool HasFavorite(IReadOnlyList<MatchItem> matches, IReadOnlyCollection<string> favorites)
        => matches.Any(match => MatchAnyFavorite(match, favorites));

    private static IReadOnlyList<string> DeduplicateExact(IEnumerable<string?> names)
        => names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
