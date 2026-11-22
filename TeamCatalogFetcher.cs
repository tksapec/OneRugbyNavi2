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
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> FetchGates = new();
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;

    public TeamCatalogFetcher() : this(Http, RequestTimeout) { }

    internal TeamCatalogFetcher(HttpClient httpClient, TimeSpan? requestTimeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _requestTimeout = requestTimeout ?? RequestTimeout;
        if (_requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    public async Task<TeamIndexFetchResult> GetAsync(int seasonStartYear, CancellationToken cancellationToken = default)
    {
        var gate = FetchGates.GetOrAdd(seasonStartYear, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await GetCoreAsync(seasonStartYear, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TeamIndexFetchResult> GetCoreAsync(int seasonStartYear, CancellationToken cancellationToken)
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
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(_requestTimeout);
            var requestToken = requestTimeout.Token;
            using var response = await _httpClient.GetAsync(teamIndexUri, HttpCompletionOption.ResponseHeadersRead, requestToken);
            response.EnsureSuccessStatusCode();
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unexpected team-index content type: {mediaType}");
            }

            var html = await response.Content.ReadAsStringAsync(requestToken);
            var parsed = TeamIndexParser.Parse(html, seasonStartYear, teamIndexUri.AbsoluteUri);
            if (!IsCompleteCatalog(parsed, seasonStartYear) || !HasBaselineCoverage(parsed, cached, seasonStartYear))
            {
                throw new InvalidDataException("The official team index did not provide a complete catalog for the requested season.");
            }

            var snapshot = new CachedTeamIndex(CacheSchemaVersion, parsed, now);
            try
            {
                await WriteCacheAsync(cachePath, snapshot, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A fresh official response is still usable when local storage fails.
            }
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

    private static bool HasBaselineCoverage(TeamIndexResult current, CachedTeamIndex? cached, int seasonStartYear)
    {
        if (!IsUsableCache(cached, seasonStartYear)) return true;
        return new[] { "DIV1", "DIV2", "DIV3" }.All(division =>
        {
            var previousCount = cached!.Snapshot.Teams.Count(team => team.DivisionCode == division);
            var currentCount = current.Teams.Count(team => team.DivisionCode == division);
            var minimumCount = Math.Max(1, (previousCount + 1) / 2);
            return currentCount >= minimumCount;
        });
    }

    private static bool IsCompleteCatalog(TeamIndexResult result, int seasonStartYear)
    {
        if (result.Readiness != SyncReadiness.Ready || result.DetectedSeasonStartYear != seasonStartYear || result.Teams.Count == 0)
        {
            return false;
        }

        var hasEveryDivision = new[] { "DIV1", "DIV2", "DIV3" }
            .All(division => result.Teams.Any(team => team.DivisionCode == division));
        var hasUniqueIds = result.Teams.Select(team => team.LeagueOneTeamId)
            .Distinct(StringComparer.Ordinal).Count() == result.Teams.Count;
        if (!hasEveryDivision || !hasUniqueIds) return false;

        // Keep the known current-season counts and logo checks as additional
        // protection for the live 2026 catalog, while applying a general
        // completeness check to every season.
        return seasonStartYear != 2026 ||
               result.Teams.Count == 27 &&
               result.Teams.Count(team => team.DivisionCode == "DIV1") == 12 &&
               result.Teams.Count(team => team.DivisionCode == "DIV2") == 8 &&
               result.Teams.Count(team => team.DivisionCode == "DIV3") == 7 &&
               HasOfficialLogos(result) &&
               result.Teams.All(team => TeamClubPageLink.TryCreateUri(team.OfficialTeamPageUrl, out _));
    }

    private static bool IsValidCachedCatalog(TeamIndexResult? result, int seasonStartYear)
    {
        if (result?.Teams is null || result.DetectedSeasonStartYear != seasonStartYear || result.Teams.Count == 0 ||
            result.Teams.Any(team => team is null || string.IsNullOrWhiteSpace(team.LeagueOneTeamId) ||
                                     string.IsNullOrWhiteSpace(team.TeamName) ||
                                     team.DivisionCode is not ("DIV1" or "DIV2" or "DIV3")))
        {
            return false;
        }

        if (result.Teams.Select(team => team.LeagueOneTeamId).Distinct(StringComparer.Ordinal).Count() != result.Teams.Count)
        {
            return false;
        }

        return IsCompleteCatalog(result, seasonStartYear) &&
               (seasonStartYear != 2026 || result.Teams.All(team => TeamClubPageLink.TryCreateUri(team.OfficialTeamPageUrl, out _)));
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
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
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
