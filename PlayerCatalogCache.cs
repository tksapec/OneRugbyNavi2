using System.Text;

namespace OneRugbyNavi2;

public static class PlayerCatalogCache
{
    public const string CacheFileName = "player-catalog-cache.json";

    public static async Task<PlayerCatalogDocument> LoadCachedOrFallbackAsync(
        string cachePath,
        Func<CancellationToken, Task<string>> loadBundledJsonAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loadBundledJsonAsync);
        var cachedCandidate = await TryLoadAsync(cachePath, cancellationToken);
        var cached = cachedCandidate is not null && HasExpectedCoverage(cachedCandidate)
            ? cachedCandidate
            : null;
        var bundledJson = await loadBundledJsonAsync(cancellationToken);
        if (!PlayerCatalogParser.TryParse(bundledJson, out var bundled, out var errors))
        {
            if (cached is not null) return cached;
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
        if (bundled.Players.Count == 0)
        {
            if (cached is not null) return cached;
            throw new InvalidDataException("The bundled player catalog contains no players.");
        }
        return cached is not null && CompareCatalogs(cached, bundled) >= 0 ? cached : bundled;
    }

    internal static int CompareCatalogs(PlayerCatalogDocument left, PlayerCatalogDocument right)
    {
        var coverage = HasCoverage(left, GetKnownTeams(left.Season))
            .CompareTo(HasCoverage(right, GetKnownTeams(right.Season)));
        if (coverage != 0) return coverage;

        var seasonRevision = SeasonStartYear(left.Season).CompareTo(SeasonStartYear(right.Season));
        if (seasonRevision != 0) return seasonRevision;

        var leftCatalogRevision = left.CatalogRevision ?? "";
        var rightCatalogRevision = right.CatalogRevision ?? "";
        if (leftCatalogRevision.Length == 0 || rightCatalogRevision.Length == 0)
            // Legacy payloads remain readable; publishing a same-season update requires a catalog revision.
            return leftCatalogRevision.Length.CompareTo(rightCatalogRevision.Length);
        return string.Compare(leftCatalogRevision, rightCatalogRevision, StringComparison.Ordinal);
    }

    private static int SeasonStartYear(string season)
        => season.Length >= 4 && int.TryParse(season.AsSpan(0, 4), out var year) ? year : 0;

    private static HashSet<string>? GetKnownTeams(string season)
        => string.Equals(season, "2026-27", StringComparison.Ordinal)
            ? SeasonCatalog.Teams2026.Select(team => team.LeagueOneTeamId).ToHashSet(StringComparer.Ordinal)
            : null;

    internal static bool HasCoverage(PlayerCatalogDocument catalog, HashSet<string>? knownTeams)
    {
        if (catalog.Players.Count == 0) return false;
        var coveredTeams = catalog.Players.Select(player => player.CurrentTeamId).ToHashSet(StringComparer.Ordinal);
        if (knownTeams is not null) return knownTeams.IsSubsetOf(coveredTeams);
        var expectedDivisions = SeasonCatalog.Teams2026.Select(team => team.DivisionCode).ToHashSet(StringComparer.Ordinal);
        var coveredDivisions = catalog.Players.Select(player => player.Division).ToHashSet(StringComparer.Ordinal);
        return expectedDivisions.IsSubsetOf(coveredDivisions);
    }

    internal static bool HasExpectedCoverage(PlayerCatalogDocument catalog)
        => HasCoverage(catalog, GetKnownTeams(catalog.Season));

    public static async Task<PlayerCatalogDocument?> TryLoadAsync(string cachePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(cachePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(cachePath, Encoding.UTF8, cancellationToken);
            return PlayerCatalogParser.TryParse(json, out var catalog, out _) ? catalog : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static async Task SaveAtomicallyAsync(string cachePath, string json, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(cachePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Cache directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
