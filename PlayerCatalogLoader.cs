namespace OneRugbyNavi2;

public sealed class PlayerCatalogLoader
{
    private const string ManifestFileName = "player-catalog-manifest.json";

    public async Task<PlayerCatalogDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cachePath = Path.Combine(FileSystem.AppDataDirectory, PlayerCatalogCache.CacheFileName);
        return await PlayerCatalogCache.LoadCachedOrFallbackAsync(cachePath, LoadBundledCatalogJsonAsync, cancellationToken);
    }

    private static async Task<string> LoadBundledCatalogJsonAsync(CancellationToken cancellationToken)
    {
        var manifestJson = await LoadBundledManifestJsonAsync(cancellationToken);
        if (!PlayerCatalogManifest.TryParse(manifestJson, out var manifest, out var error))
            throw new InvalidDataException(error);

        if (!manifest.TryGetPackageAssetPath(out var assetPath))
            throw new InvalidDataException("Manifest catalog path is not a valid package asset path.");

        return await ReadPackageFileAsync(assetPath, cancellationToken);
    }

    public static Task<string> LoadBundledManifestJsonAsync(CancellationToken cancellationToken = default)
        => ReadPackageFileAsync(ManifestFileName, cancellationToken);

    private static async Task<string> ReadPackageFileAsync(string fileName, CancellationToken cancellationToken)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
