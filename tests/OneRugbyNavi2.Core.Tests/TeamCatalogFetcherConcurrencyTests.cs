using System.Net;
using System.Text;
using Xunit;

namespace OneRugbyNavi2.Core.Tests;

public sealed class TeamCatalogFetcherConcurrencyTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{\"readiness\":0,\"detectedSeasonStartYear\":2026,\"teams\":null}")]
    [InlineData("{\"readiness\":0,\"detectedSeasonStartYear\":2026,\"teams\":[null]}")]
    public async Task Invalid_cached_snapshot_falls_back_after_network_failure(string snapshot)
    {
        var directory = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "OneRugbyNavi2");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "team-index-2026.json");
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":3,\"snapshot\":" + snapshot + ",\"updatedAt\":\"2026-01-01T00:00:00Z\"}");
        try
        {
            using var client = new HttpClient(new FailingHandler());
            var result = await new TeamCatalogFetcher(client).GetAsync(2026);
            Assert.True(result.UsedFallback);
            Assert.NotEmpty(result.Snapshot.Teams);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Incomplete_cached_catalog_is_rejected_for_non_2026_seasons()
    {
        var directory = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "OneRugbyNavi2");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "team-index-2024.json");
        await File.WriteAllTextAsync(path,
            "{\"schemaVersion\":3,\"snapshot\":{\"readiness\":0,\"detectedSeasonStartYear\":2024,\"teams\":[{\"leagueOneTeamId\":\"101\",\"teamName\":\"Club A\",\"divisionCode\":\"DIV1\",\"teamUrl\":\"https://league-one.jp/team/101\"}]},\"updatedAt\":\"2026-01-01T00:00:00Z\"}");
        try
        {
            using var client = new HttpClient(new FailingHandler());
            var result = await new TeamCatalogFetcher(client).GetAsync(2024);

            Assert.True(result.UsedFallback);
            Assert.Empty(result.Snapshot.Teams);
        }
        finally { File.Delete(path); }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("simulated offline");
    }

    [Fact]
    public async Task Same_season_fetches_share_cache_and_cancelled_waiter_does_not_release_gate()
    {
        var handler = new BlockingHandler();
        using var client = new HttpClient(handler);
        var firstFetcher = new TeamCatalogFetcher(client);
        var secondFetcher = new TeamCatalogFetcher(client);
        var first = firstFetcher.GetAsync(2024);
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var cancelled = secondFetcher.GetAsync(2024, cancellation.Token);
        var second = secondFetcher.GetAsync(2024);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(second.IsCompleted);
        handler.Release.TrySetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, handler.Calls);
        Assert.False(results[0].UsedFallback);
        Assert.True(results[1].UsedFallback);
    }

    [Fact]
    public async Task Fresh_complete_catalog_is_returned_when_cache_write_fails()
    {
        var cacheDirectory = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "OneRugbyNavi2");
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, "team-index-2024.json");
        Directory.CreateDirectory(cachePath);
        try
        {
            using var client = new HttpClient(new CatalogHandler(CompleteCatalogHtml));
            var result = await new TeamCatalogFetcher(client).GetAsync(2024);

            Assert.False(result.UsedFallback);
            Assert.Equal(3, result.Snapshot.Teams.Count);
        }
        finally
        {
            Directory.Delete(cachePath, recursive: true);
        }
    }

    [Fact]
    public async Task Catalog_missing_a_division_is_rejected_for_any_season()
    {
        using var client = new HttpClient(new CatalogHandler(
            "<html><body><h2>2024-25シーズン</h2><section id='division-1'><h2>DIVISION 1</h2><a href='/team/101'><span class='name'>Club A</span></a></section></body></html>"));

        var result = await new TeamCatalogFetcher(client).GetAsync(2024);

        Assert.True(result.UsedFallback);
        Assert.Empty(result.Snapshot.Teams);
    }

    [Fact]
    public async Task Thin_same_season_response_preserves_complete_cached_division_coverage()
    {
        var directory = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "OneRugbyNavi2");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "team-index-2024.json");
        var baselineTeams = new[] { "DIV1", "DIV2", "DIV3" }
            .SelectMany((division, index) => Enumerable.Range(1, 4).Select(number => new TeamIndexEntry(
                $"{index}{number}", $"{division} Club {number}", division,
                $"https://league-one.jp/team/{index}{number}", "", "")))
            .ToArray();
        var cached = new
        {
            schemaVersion = 3,
            snapshot = new TeamIndexResult(SyncReadiness.Ready, 2024, baselineTeams),
            updatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(cached, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        try
        {
            using var client = new HttpClient(new CatalogHandler(CompleteCatalogHtml));
            var result = await new TeamCatalogFetcher(client).GetAsync(2024);

            Assert.True(result.UsedFallback);
            Assert.Equal(12, result.Snapshot.Teams.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Request_deadline_covers_response_body_read()
    {
        using var client = new HttpClient(new SlowBodyHandler());
        var fetcher = new TeamCatalogFetcher(client, TimeSpan.FromMilliseconds(50));

        var result = await fetcher.GetAsync(2024).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(result.UsedFallback);
        Assert.Empty(result.Snapshot.Teams);
    }

    private const string CompleteCatalogHtml = "<html><body><h2>2024-25シーズン</h2><section id='division-1'><h2>DIVISION 1</h2><a href='/team/101'><span class='name'>Club A</span></a></section><section id='division-2'><h2>DIVISION 2</h2><a href='/team/201'><span class='name'>Club B</span></a></section><section id='division-3'><h2>DIVISION 3</h2><a href='/team/301'><span class='name'>Club C</span></a></section></body></html>";

    private sealed class CatalogHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
    }

    private sealed class SlowBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new SlowBodyContent()
            });
    }

    private sealed class SlowBodyContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => Task.Delay(Timeout.InfiniteTimeSpan);

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
            => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CompleteCatalogHtml, Encoding.UTF8, "text/html") };
        }
    }
}
