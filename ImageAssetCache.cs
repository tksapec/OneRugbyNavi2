using System.Security.Cryptography;

namespace OneRugbyNavi2;

public static class ImageAssetCache
{
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly SemaphoreSlim DownloadGate = new(4, 4);

    public static string? GetCachedPath(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId) || entityId.Any(character => !char.IsAsciiDigit(character)))
        {
            return null;
        }

        return FindExistingValidImage(Path.Combine(
            LeagueOneDatabase.AppDataRoot,
            "cache",
            "images",
            "teams",
            entityId));
    }

    public static async Task<string?> FetchAndCacheAsync(
        string sourceUrl,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(entityId) ||
            entityId.Any(character => !char.IsAsciiDigit(character)))
        {
            return null;
        }

        var entityDirectory = Path.Combine(LeagueOneDatabase.AppDataRoot, "cache", "images", "teams", entityId);
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
                    return FindExistingValidImage(entityDirectory);
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
                        return FindExistingValidImage(entityDirectory);
                    }

                    await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
                }

                var bytes = buffer.ToArray();
                var extension = ImageSignatureValidator.GetExtension(bytes);
                if (extension is null)
                {
                    return FindExistingValidImage(entityDirectory);
                }

                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var targetPath = Path.Combine(entityDirectory, $"{hash}{extension}");
                if (File.Exists(targetPath)) return targetPath;

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

                    return File.Exists(targetPath) ? targetPath : null;
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
            return FindExistingValidImage(entityDirectory);
        }
    }

    private static string? FindExistingValidImage(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            Span<byte> signature = stackalloc byte[12];
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                try
                {
                    var expectedExtension = Path.GetExtension(path).ToLowerInvariant();
                    if (expectedExtension is not (".png" or ".jpg" or ".gif" or ".webp")) continue;
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Length <= 0 || fileInfo.Length > MaxImageBytes) continue;
                    using var stream = File.OpenRead(path);
                    var count = stream.Read(signature);
                    if (string.Equals(ImageSignatureValidator.GetExtension(signature[..count]), expectedExtension, StringComparison.Ordinal)) return path;
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
}
