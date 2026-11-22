using Xunit;

namespace Microsoft.Maui.Storage
{
    public static class FileSystem
    {
        public static string AppDataDirectory { get; } = Path.Combine(Path.GetTempPath(), "OneRugbyNavi2-tests-" + Guid.NewGuid().ToString("N"));
        public static int FailNextPackageOpen;
        public static string ManifestContent = "";
        public static string? FailPackagePath;
        public static Task<Stream> OpenAppPackageFileAsync(string path)
        {
            if (Interlocked.Exchange(ref FailNextPackageOpen, 0) != 0) throw new IOException("Simulated package read failure.");
            if (path == FailPackagePath) throw new IOException("Simulated asset read failure.");
            return Task.FromResult<Stream>(path == "cache_manifest.txt"
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ManifestContent))
                : path == "cache/test-asset.bin"
                    ? new MemoryStream([1, 2, 3, 4])
                    : File.OpenRead(Path.Combine("Fixtures", path)));
        }
    }
}

namespace OneRugbyNavi2.Core.Tests
{
    public sealed class ScheduleCacheStoreTests
    {
        [Fact]
        public async Task Partial_cache_round_trips_and_full_refresh_clears_partial_flag()
        {
            var key = Guid.NewGuid().ToString("N");
            var date = DateTimeOffset.UtcNow;
            var matches = new[] { new ScheduleFetcher.Item { Home = "Team A", Away = "Team B" } };
            try
            {
                await ScheduleCacheStore.SaveAsync(key, key, matches, [], [], [], [], date, isPartial: true);
                var partial = await ScheduleCacheStore.LoadAsync(key, key);
                Assert.NotNull(partial);
                Assert.True(partial.IsPartial);
                Assert.Single(partial.Div1);

                await ScheduleCacheStore.SaveAsync(key, key, matches, [], [], [], [], date, isPartial: false);
                var full = await ScheduleCacheStore.LoadAsync(key, key);
                Assert.NotNull(full);
                Assert.False(full.IsPartial);
            }
            finally
            {
                File.Delete(ScheduleCacheStore.GetCachePath(key, key));
            }
        }

        [Fact]
        public async Task Partial_refresh_keeps_fresh_rows_when_persisting_cache_merge()
        {
            var key = Guid.NewGuid().ToString("N");
            var fresh = new ScheduleFetcher.Item { SeasonStartYear = 2026, Home = "Fresh A", Away = "Fresh B", Date = "12月2日" };
            var cached = new ScheduleFetcher.Item { SeasonStartYear = 2026, Home = "Cached A", Away = "Cached B", Date = "12月3日" };
            try
            {
                var merged = ScheduleFetcher.SelectDivisionWithCache([fresh], [cached], "annual page failed");
                await ScheduleCacheStore.SaveAsync(key, key, merged, [], [], [], [], DateTimeOffset.UtcNow, isPartial: true);

                var loaded = await ScheduleCacheStore.LoadAsync(key, key);

                Assert.NotNull(loaded);
                Assert.True(loaded.IsPartial);
                Assert.Contains(loaded.Div1, item => item.Home == "Fresh A");
                Assert.Contains(loaded.Div1, item => item.Home == "Cached A");
            }
            finally
            {
                File.Delete(ScheduleCacheStore.GetCachePath(key, key));
            }
        }
    }

    public sealed class LeagueOneDatabaseConcurrencyTests
    {
        [Fact]
        public async Task Concurrent_initializations_do_not_collide_on_seed_file()
        {
            var database = new LeagueOneDatabase();
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => database.InitializeAsync()));
            Assert.True(File.Exists(database.GetDatabasePath()));
        }

        [Fact]
        public async Task Initialization_can_retry_after_package_read_failure()
        {
            var database = new LeagueOneDatabase();
            Microsoft.Maui.Storage.FileSystem.FailNextPackageOpen = 1;
            await Assert.ThrowsAsync<IOException>(() => database.InitializeAsync());
            await database.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(File.Exists(database.GetDatabasePath()));
        }

        [Fact]
        public async Task Missing_cache_marker_replaces_an_incomplete_existing_asset()
        {
            var database = new LeagueOneDatabase();
            var cacheDirectory = Path.Combine(LeagueOneDatabase.AppDataRoot, "cache");
            var assetPath = Path.Combine(cacheDirectory, "test-asset.bin");
            var markerPath = Path.Combine(cacheDirectory, ".cache_seed_version");
            Directory.CreateDirectory(cacheDirectory);
            await File.WriteAllBytesAsync(assetPath, [9]);
            File.Delete(markerPath);
            Microsoft.Maui.Storage.FileSystem.ManifestContent = "cache/test-asset.bin\n";
            try
            {
                await database.InitializeAsync();
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(assetPath));
                Assert.True(File.Exists(markerPath));
            }
            finally
            {
                Microsoft.Maui.Storage.FileSystem.ManifestContent = "";
            }
        }

        [Fact]
        public async Task Failed_forced_refresh_invalidates_marker_and_retries_assets()
        {
            var database = new LeagueOneDatabase();
            var cacheDirectory = Path.Combine(LeagueOneDatabase.AppDataRoot, "cache");
            var assetPath = Path.Combine(cacheDirectory, "test-asset.bin");
            var markerPath = Path.Combine(cacheDirectory, ".cache_seed_version");
            Microsoft.Maui.Storage.FileSystem.ManifestContent = "cache/test-asset.bin\n";
            try
            {
                await database.InitializeAsync();
                await File.WriteAllBytesAsync(database.GetDatabasePath(), [0, 1, 2]);
                await File.WriteAllBytesAsync(assetPath, [9]);
                Microsoft.Maui.Storage.FileSystem.FailPackagePath = "cache/test-asset.bin";
                await Assert.ThrowsAsync<IOException>(() => database.InitializeAsync());
                Assert.False(File.Exists(markerPath));

                Microsoft.Maui.Storage.FileSystem.FailPackagePath = null;
                await database.InitializeAsync();
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(assetPath));
                Assert.True(File.Exists(markerPath));
            }
            finally
            {
                Microsoft.Maui.Storage.FileSystem.FailPackagePath = null;
                Microsoft.Maui.Storage.FileSystem.ManifestContent = "";
            }
        }
    }
}
