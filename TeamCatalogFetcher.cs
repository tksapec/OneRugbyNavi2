using System.Text.Json;

namespace OneRugbyNavi2;

public sealed class TeamCatalogFetcher
{
    private const int CacheSchemaVersion = 3;

    private static Uri GetTeamIndexUri(int seasonStartYear)
        => seasonStartYear == 2026
            ? new Uri("https://league-one.jp/content/team/2026")
            : new Uri($"https://league-one.jp/team/?year={seasonStartYear}");
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TeamIndexFetchResult> GetAsync(int seasonStartYear, CancellationToken cancellationToken = default)
    {
        var cachePath = GetCachePath(seasonStartYear);
        var teamIndexUri = GetTeamIndexUri(seasonStartYear);
        var cached = await ReadCacheAsync(cachePath, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (IsUsableCache(cached, seasonStartYear) && now - cached!.UpdatedAt < CacheTtl)
        {
            return new TeamIndexFetchResult(cached.Snapshot, true, cached.UpdatedAt);
        }

        try
        {
            using var response = await Http.GetAsync(teamIndexUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unexpected team-index content type: {mediaType}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = TeamIndexParser.Parse(html, seasonStartYear, teamIndexUri.AbsoluteUri);
            if (!IsCompleteCatalog(parsed, seasonStartYear))
            {
                throw new InvalidDataException("The official team index did not provide a complete catalog for the requested season.");
            }

            var snapshot = new CachedTeamIndex(CacheSchemaVersion, parsed, now);
            await WriteCacheAsync(cachePath, snapshot, cancellationToken);
            return new TeamIndexFetchResult(parsed, false, now);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (IsUsableCache(cached, seasonStartYear))
            {
                return new TeamIndexFetchResult(cached!.Snapshot, true, cached.UpdatedAt);
            }

            var fallback = BuildFallback(seasonStartYear);
            return new TeamIndexFetchResult(fallback, true, DateTimeOffset.MinValue);
        }
    }

    private static bool IsUsableCache(CachedTeamIndex? cached, int seasonStartYear)
        => cached is { SchemaVersion: CacheSchemaVersion } && IsValidCachedCatalog(cached.Snapshot, seasonStartYear);

    private static bool IsCompleteCatalog(TeamIndexResult result, int seasonStartYear)
    {
        if (result.Readiness != SyncReadiness.Ready || result.DetectedSeasonStartYear != seasonStartYear || result.Teams.Count == 0)
        {
            return false;
        }

        if (seasonStartYear != 2026) return true;
        return result.Teams.Count == 27 &&
               result.Teams.Count(team => team.DivisionCode == "DIV1") == 12 &&
               result.Teams.Count(team => team.DivisionCode == "DIV2") == 8 &&
               result.Teams.Count(team => team.DivisionCode == "DIV3") == 7 &&
               HasOfficialLogos(result) &&
               result.Teams.All(team => TeamClubPageLink.TryCreateUri(team.OfficialTeamPageUrl, out _));
    }

    private static bool IsValidCachedCatalog(TeamIndexResult result, int seasonStartYear)
    {
        if (result.DetectedSeasonStartYear != seasonStartYear || result.Teams.Count == 0 ||
            result.Teams.Any(team => string.IsNullOrWhiteSpace(team.LeagueOneTeamId) ||
                                     string.IsNullOrWhiteSpace(team.TeamName) ||
                                     team.DivisionCode is not ("DIV1" or "DIV2" or "DIV3")))
        {
            return false;
        }

        if (result.Teams.Select(team => team.LeagueOneTeamId).Distinct(StringComparer.Ordinal).Count() != result.Teams.Count)
        {
            return false;
        }

        return seasonStartYear != 2026 || IsCompleteCatalog(result, seasonStartYear);
    }

    private static bool HasOfficialLogos(TeamIndexResult result)
        => result.Teams.All(team =>
            Uri.TryCreate(team.LogoUrl, UriKind.Absolute, out var logoUri) &&
            logoUri.Scheme == Uri.UriSchemeHttps &&
            (logoUri.Host.Equals("league-one.s3.ap-northeast-1.amazonaws.com", StringComparison.OrdinalIgnoreCase) ||
             logoUri.Host.Equals("league-one.s3-ap-northeast-1.amazonaws.com", StringComparison.OrdinalIgnoreCase)));

    private static TeamIndexResult BuildFallback(int seasonStartYear)
    {
        if (seasonStartYear != 2026)
        {
            return new TeamIndexResult(SyncReadiness.NotPublished, 0, Array.Empty<TeamIndexEntry>());
        }

        var teams = SeasonCatalog.Teams2026.Select(team => new TeamIndexEntry(
            team.LeagueOneTeamId,
            team.TeamName,
            team.DivisionCode,
            team.TeamUrl,
            team.LogoUrl)).ToList();
        return new TeamIndexResult(SyncReadiness.Partial, seasonStartYear, teams);
    }

    private static string GetCachePath(int seasonStartYear)
        => Path.Combine(FileSystem.AppDataDirectory, "OneRugbyNavi2", $"team-index-{seasonStartYear}.json");

    private static async Task<CachedTeamIndex?> ReadCacheAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path)) return null;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<CachedTeamIndex>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string path, CachedTeamIndex value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private sealed record CachedTeamIndex(int SchemaVersion, TeamIndexResult Snapshot, DateTimeOffset UpdatedAt);
}
