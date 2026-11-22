using System.Security.Cryptography;
using System.Text.Json;

namespace OneRugbyNavi2;

public static class ImageAssetCache
{
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(90);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly SemaphoreSlim DownloadGate = new(4, 4);

    public static string? GetCachedPath(string entityId)
    {
        if (!IsValidEntityId(entityId)) return null;
        var directory = GetEntityDirectory(entityId);
        var metadata = ReadMetadata(directory);
        if (metadata is not null && IsMetadataPathValid(directory, metadata) && IsValidImage(metadata.FilePath))
        {
            return metadata.FilePath;
        }

        return FindExistingValidImage(directory);
    }

    public static string? GetCachedPath(string entityId, string sourceUrl)
    {
        return GetCachedPathCore(entityId, sourceUrl, allowExpired: false);
    }

    public static string? GetCachedPathForFallback(string entityId, string? sourceUrl)
    {
        if (!IsValidEntityId(entityId)) return null;
        if (string.IsNullOrWhiteSpace(sourceUrl)) return GetCachedPath(entityId);

        var directory = GetEntityDirectory(entityId);
        var metadata = ReadMetadata(directory);
        if (metadata is null) return FindExistingValidImage(directory);
        if (!IsMetadataPathValid(directory, metadata) ||
            !string.Equals(metadata.SourceUrl, sourceUrl, StringComparison.Ordinal))
        {
            return null;
        }

        return IsValidImage(metadata.FilePath) ? metadata.FilePath : null;
    }

    public static async Task<string?> FetchAndCacheAsync(
        string sourceUrl,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !IsValidEntityId(entityId))
        {
            return null;
        }

        var entityDirectory = GetEntityDirectory(entityId);
        try
        {
            Directory.CreateDirectory(entityDirectory);
            await DownloadGate.WaitAsync(cancellationToken);
            try
            {
                using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > MaxImageBytes)
                {
                    return FindFallback(entityId, sourceUrl);
                }

                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var buffer = new MemoryStream();
                var chunk = new byte[16 * 1024];
                while (true)
                {
                    var count = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
                    if (count == 0) break;
                    if (buffer.Length + count > MaxImageBytes)
                    {
                        return FindFallback(entityId, sourceUrl);
                    }

                    await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
                }

                var bytes = buffer.ToArray();
                var extension = ImageSignatureValidator.GetExtension(bytes);
                if (extension is null)
                {
                    return FindFallback(entityId, sourceUrl);
                }

                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var targetPath = Path.Combine(entityDirectory, $"{hash}{extension}");
                if (File.Exists(targetPath))
                {
                    await WriteMetadataAsync(entityDirectory, sourceUrl, targetPath, cancellationToken);
                    return targetPath;
                }

                var temporaryPath = Path.Combine(entityDirectory, $".{Guid.NewGuid():N}.tmp");
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
                    try
                    {
                        File.Move(temporaryPath, targetPath);
                    }
                    catch (IOException) when (File.Exists(targetPath))
                    {
                        // Another request stored the same verified content.
                    }

                    if (!File.Exists(targetPath)) return null;
                    await WriteMetadataAsync(entityDirectory, sourceUrl, targetPath, cancellationToken);
                    return targetPath;
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            finally
            {
                DownloadGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return FindFallback(entityId, sourceUrl);
        }
    }

    private static string GetEntityDirectory(string entityId)
        => Path.Combine(LeagueOneDatabase.AppDataRoot, "cache", "images", "teams", entityId);

    private static bool IsValidEntityId(string entityId)
        => !string.IsNullOrWhiteSpace(entityId) && entityId.All(char.IsAsciiDigit);

    private static string? FindFallback(string entityId, string sourceUrl)
        => GetCachedPathForFallback(entityId, sourceUrl);

    private static string? GetCachedPathCore(string entityId, string sourceUrl, bool allowExpired)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl) || !IsValidEntityId(entityId)) return null;
        var directory = GetEntityDirectory(entityId);
        var metadata = ReadMetadata(directory);
        if (metadata is null || !IsMetadataPathValid(directory, metadata) ||
            !string.Equals(metadata.SourceUrl, sourceUrl, StringComparison.Ordinal) ||
            !allowExpired && DateTimeOffset.UtcNow - metadata.CachedAt > CacheMaxAge)
        {
            return null;
        }

        return IsValidImage(metadata.FilePath) ? metadata.FilePath : null;
    }

    private static CacheMetadata? ReadMetadata(string directory)
    {
        try
        {
            var metadataPath = Path.Combine(directory, "source.json");
            return File.Exists(metadataPath)
                ? JsonSerializer.Deserialize<CacheMetadata>(File.ReadAllText(metadataPath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMetadataPathValid(string directory, CacheMetadata metadata)
        => !string.IsNullOrWhiteSpace(metadata.SourceUrl) &&
           !string.IsNullOrWhiteSpace(metadata.FilePath) &&
           string.Equals(Path.GetDirectoryName(metadata.FilePath), directory, StringComparison.OrdinalIgnoreCase);

    private static async Task WriteMetadataAsync(string directory, string sourceUrl, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var metadataPath = Path.Combine(directory, "source.json");
            var tempPath = metadataPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(new CacheMetadata(sourceUrl, DateTimeOffset.UtcNow, filePath)), cancellationToken);
                File.Move(tempPath, metadataPath, true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The downloaded image remains usable even if metadata storage fails.
        }
    }

    private static string? FindExistingValidImage(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            foreach (var path in Directory.EnumerateFiles(directory).OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                try
                {
                    if (IsValidImage(path)) return path;
                }
                catch
                {
                    // Keep failed or unreadable files intact; simply do not use them.
                }
            }
        }
        catch
        {
            // Cache failure must not remove or block the fallback badge.
        }

        return null;
    }

    private static bool IsValidImage(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var expectedExtension = Path.GetExtension(path).ToLowerInvariant();
            if (expectedExtension is not (".png" or ".jpg" or ".gif" or ".webp")) return false;
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaxImageBytes) return false;
            Span<byte> signature = stackalloc byte[12];
            using var stream = File.OpenRead(path);
            var count = stream.Read(signature);
            return string.Equals(ImageSignatureValidator.GetExtension(signature[..count]), expectedExtension, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private sealed record CacheMetadata(string SourceUrl, DateTimeOffset CachedAt, string FilePath);
}
