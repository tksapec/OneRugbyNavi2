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
        var cached = await TryLoadAsync(cachePath, cancellationToken);
        if (cached is not null) return cached;

        var bundledJson = await loadBundledJsonAsync(cancellationToken);
        if (!PlayerCatalogParser.TryParse(bundledJson, out var bundled, out var errors))
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return bundled;
    }

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
