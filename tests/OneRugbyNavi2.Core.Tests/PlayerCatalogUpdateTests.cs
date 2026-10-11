using OneRugbyNavi2;
using System.Net;
using System.Text;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class PlayerCatalogManifestTests
{
    [Fact]
    public void Parses_supported_manifest_and_current_catalog_path()
    {
        const string json = "{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/league-one-players-2026-27.json\"}";

        Assert.True(PlayerCatalogManifest.TryParse(json, out var manifest, out var error), error);
        Assert.Equal(1, manifest.FormatVersion);
        Assert.Equal("Resources/Raw/league-one-players-2026-27.json", manifest.CatalogPath);
    }

    [Fact]
    public void Resolves_nested_catalog_path_to_package_asset_path()
    {
        const string json = "{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/archive/catalog.json\"}";

        Assert.True(PlayerCatalogManifest.TryParse(json, out var manifest, out var error), error);
        Assert.True(manifest.TryGetPackageAssetPath(out var assetPath));
        Assert.Equal("archive/catalog.json", assetPath);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"formatVersion\":2,\"catalogPath\":\"Resources/Raw/players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"https://evil.example/players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"/Resources/Raw/players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/../players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw//players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/players.json?x=1\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/players.json#part\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Resources\\Raw\\players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Other/players.json\"}")]
    [InlineData("{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/players.txt\"}")]
    public void Rejects_invalid_manifest(string json)
    {
        Assert.False(PlayerCatalogManifest.TryParse(json, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}

public sealed class PlayerCatalogUpdateServiceTests
{
    private const string ManifestJson = "{\"formatVersion\":1,\"catalogPath\":\"Resources/Raw/league-one-players-2026-27.json\"}";

    [Fact]
    public async Task Update_downloads_manifest_path_and_saves_valid_catalog()
    {
        var catalogJson = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var handler = new QueueHandler(Reply(HttpStatusCode.OK, ManifestJson), Reply(HttpStatusCode.OK, catalogJson));
        var service = CreateService(handler, Path.Combine(temp.Path, "players.json"));

        var result = await service.UpdateAsync();

        Assert.True(result.Changed);
        Assert.NotEmpty(result.Catalog.Players);
        Assert.Equal(catalogJson, await File.ReadAllTextAsync(Path.Combine(temp.Path, "players.json")));
        Assert.Equal("https://raw.githubusercontent.com/tksapec/OneRugbyNavi2/main/Resources/Raw/player-catalog-manifest.json", handler.Requests[0]);
        Assert.Equal("https://raw.githubusercontent.com/tksapec/OneRugbyNavi2/main/Resources/Raw/league-one-players-2026-27.json", handler.Requests[1]);
    }

    [Fact]
    public async Task Update_uses_bundled_manifest_when_remote_manifest_is_not_published_yet()
    {
        var fallbackManifest = await File.ReadAllTextAsync(Path.Combine("Fixtures", "player-catalog-manifest.json"));
        var catalogJson = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var handler = new QueueHandler(Reply(HttpStatusCode.NotFound, ""), Reply(HttpStatusCode.OK, catalogJson));

        var result = await CreateService(handler, Path.Combine(temp.Path, "players.json"))
            .UpdateAsync(fallbackManifest);

        Assert.True(result.UsedBundledManifest);
        Assert.True(result.Changed);
        Assert.Equal("https://raw.githubusercontent.com/tksapec/OneRugbyNavi2/main/Resources/Raw/league-one-players-2026-27.json", handler.Requests[1]);
    }

    [Fact]
    public async Task Bundled_manifest_points_to_current_catalog_file()
    {
        var json = await File.ReadAllTextAsync(Path.Combine("Fixtures", "player-catalog-manifest.json"));

        Assert.True(PlayerCatalogManifest.TryParse(json, out var manifest, out var error), error);
        Assert.Equal("Resources/Raw/league-one-players-2026-27.json", manifest.CatalogPath);
    }

    [Fact]
    public async Task Update_reports_unchanged_catalog_without_rewriting_it()
    {
        var catalogJson = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, catalogJson);
        var originalWriteTime = File.GetLastWriteTimeUtc(cachePath);
        var handler = new QueueHandler(Reply(HttpStatusCode.OK, ManifestJson), Reply(HttpStatusCode.OK, catalogJson));

        var result = await CreateService(handler, cachePath).UpdateAsync();

        Assert.False(result.Changed);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(cachePath));
        Assert.True(result.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{\"schemaVersion\":1,\"season\":\"2026-27\",\"players\":[]}")]
    [InlineData(HttpStatusCode.OK, "oversized")]
    public async Task Update_failure_preserves_existing_cache(HttpStatusCode catalogStatus, string catalogResponse)
    {
        var original = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, original);
        var response = catalogResponse == "oversized" ? new string('x', 20 * 1024 * 1024 + 1) : catalogResponse;
        var handler = new QueueHandler(Reply(HttpStatusCode.OK, ManifestJson), Reply(catalogStatus, response));

        await Assert.ThrowsAnyAsync<Exception>(() => CreateService(handler, cachePath).UpdateAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task Update_failure_to_fetch_manifest_preserves_existing_cache()
    {
        var original = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, original);

        await Assert.ThrowsAnyAsync<Exception>(() => CreateService(
            new QueueHandler(Reply(HttpStatusCode.ServiceUnavailable, "")), cachePath).UpdateAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task Update_invalid_remote_manifest_preserves_existing_cache()
    {
        var original = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, original);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService(
            new QueueHandler(Reply(HttpStatusCode.OK, "not json")), cachePath).UpdateAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task Update_oversized_remote_manifest_preserves_existing_cache()
    {
        var original = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, original);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService(
            new QueueHandler(Reply(HttpStatusCode.OK, new string('x', 65 * 1024))), cachePath).UpdateAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task Update_rejects_redirect_to_another_url_and_preserves_cache()
    {
        var original = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, original);
        var redirect = Reply(HttpStatusCode.OK, "{}");
        redirect.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.example/manifest.json");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateService(
            new QueueHandler(redirect), cachePath).UpdateAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task Update_times_out_and_preserves_existing_cache()
    {
        var original = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, original);
        var service = new PlayerCatalogUpdateService(
            new HttpClient(new BlockingHandler()),
            PlayerCatalogUpdateService.DefaultManifestUri,
            cachePath,
            TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UpdateAsync());

        Assert.Equal(original, await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public void Update_does_not_capture_callers_synchronization_context()
    {
        var catalogJson = File.ReadAllText(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new TemporaryDirectory();
        var handler = new AsyncQueueHandler(Reply(HttpStatusCode.OK, ManifestJson), Reply(HttpStatusCode.OK, catalogJson));
        var previousContext = SynchronizationContext.Current;
        var context = new CountingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
#pragma warning disable xUnit1031
            var result = CreateService(handler, Path.Combine(temp.Path, "players.json"))
                .UpdateAsync().GetAwaiter().GetResult();
#pragma warning restore xUnit1031

            Assert.NotEmpty(result.Catalog.Players);
            Assert.Equal(0, context.PostCount);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static PlayerCatalogUpdateService CreateService(HttpMessageHandler handler, string cachePath)
        => new(new HttpClient(handler), PlayerCatalogUpdateService.DefaultManifestUri, cachePath);

    private static HttpResponseMessage Reply(HttpStatusCode statusCode, string body)
        => new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            var response = _responses.Dequeue();
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class AsyncQueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _responses.Dequeue();
            return Task.Run(async () =>
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                response.RequestMessage ??= request;
                return response;
            }, cancellationToken);
        }
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;
        public int PostCount => _postCount;

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }

    public sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

public sealed class PlayerCatalogCacheTests
{
    [Fact]
    public async Task Load_cached_catalog_without_calling_bundled_fallback()
    {
        var bundled = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new PlayerCatalogUpdateServiceTests.TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, bundled);
        var fallbackCalled = false;

        var catalog = await PlayerCatalogCache.LoadCachedOrFallbackAsync(cachePath, _ =>
        {
            fallbackCalled = true;
            return Task.FromResult(bundled);
        });

        Assert.False(fallbackCalled);
        Assert.Equal("2026-27", catalog.Season);
    }

    [Fact]
    public async Task Load_bundled_catalog_when_cache_is_missing_or_invalid()
    {
        var bundled = await File.ReadAllTextAsync(Path.Combine("Fixtures", "league-one-players-2026-27.json"));
        using var temp = new PlayerCatalogUpdateServiceTests.TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, "invalid");

        var catalog = await PlayerCatalogCache.LoadCachedOrFallbackAsync(cachePath, _ => Task.FromResult(bundled));

        Assert.Equal("2026-27", catalog.Season);
    }

    [Fact]
    public async Task Cancelled_atomic_write_preserves_existing_cache()
    {
        using var temp = new PlayerCatalogUpdateServiceTests.TemporaryDirectory();
        var cachePath = Path.Combine(temp.Path, "players.json");
        await File.WriteAllTextAsync(cachePath, "last valid catalog");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PlayerCatalogCache.SaveAtomicallyAsync(cachePath, "new catalog", cancellation.Token));

        Assert.Equal("last valid catalog", await File.ReadAllTextAsync(cachePath));
        Assert.Empty(Directory.GetFiles(temp.Path, "players.json.tmp-*"));
    }
}
