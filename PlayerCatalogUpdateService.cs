using System.Text;
using System.Net;

namespace OneRugbyNavi2;

public sealed class PlayerCatalogUpdateService
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumCatalogBytes = 16 * 1024 * 1024;
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly Uri RawRepositoryBase = new("https://raw.githubusercontent.com/tksapec/OneRugbyNavi2/main/");

    private readonly HttpClient _httpClient;
    private readonly Uri _manifestUri;
    private readonly string _cachePath;
    private readonly TimeSpan _requestTimeout;

    public static Uri DefaultManifestUri { get; } = new(
        "https://raw.githubusercontent.com/tksapec/OneRugbyNavi2/main/Resources/Raw/player-catalog-manifest.json");

    public PlayerCatalogUpdateService(HttpClient httpClient, Uri manifestUri, string cachePath, TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(manifestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (!manifestUri.Equals(DefaultManifestUri))
            throw new ArgumentException("Manifest URI must be the configured repository manifest.", nameof(manifestUri));
        if (requestTimeout is { } timeoutValue && timeoutValue <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));

        _httpClient = httpClient;
        _manifestUri = manifestUri;
        _cachePath = cachePath;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    public Task<PlayerCatalogUpdateResult> UpdateAsync(CancellationToken cancellationToken = default)
        => UpdateAsync(bundledManifestJson: null, cancellationToken);

    public async Task<PlayerCatalogUpdateResult> UpdateAsync(
        string? bundledManifestJson,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);

        var usedBundledManifest = false;
        string manifestJson;
        try
        {
            manifestJson = await FetchTextAsync(_manifestUri, MaximumManifestBytes, timeout.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && bundledManifestJson is not null)
        {
            manifestJson = bundledManifestJson;
            usedBundledManifest = true;
        }

        if (!PlayerCatalogManifest.TryParse(manifestJson, out var manifest, out var manifestError))
            throw new InvalidDataException(manifestError);

        var encodedPath = string.Join('/', manifest.CatalogPath.Split('/').Select(Uri.EscapeDataString));
        var catalogUri = new Uri(RawRepositoryBase, encodedPath);
        var catalogJson = await FetchTextAsync(catalogUri, MaximumCatalogBytes, timeout.Token).ConfigureAwait(false);
        if (!PlayerCatalogParser.TryParse(catalogJson, out var catalog, out var errors))
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        if (catalog.Players.Count == 0)
            throw new InvalidDataException("The player catalog contains no players.");

        var previousJson = File.Exists(_cachePath)
            ? await File.ReadAllTextAsync(_cachePath, Encoding.UTF8, timeout.Token).ConfigureAwait(false)
            : null;
        var changed = !string.Equals(previousJson, catalogJson, StringComparison.Ordinal);
        if (changed)
            await PlayerCatalogCache.SaveAtomicallyAsync(_cachePath, catalogJson, timeout.Token).ConfigureAwait(false);

        return new PlayerCatalogUpdateResult(catalog, DateTimeOffset.UtcNow, changed, usedBundledManifest);
    }

    private async Task<string> FetchTextAsync(Uri uri, int maximumBytes, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri ?? uri;
        if (finalUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(finalUri.GetLeftPart(UriPartial.Authority), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(finalUri.AbsolutePath, uri.AbsolutePath, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(finalUri.UserInfo) || !string.IsNullOrEmpty(finalUri.Query) || !string.IsNullOrEmpty(finalUri.Fragment))
            throw new InvalidDataException("GitHub returned content from an unexpected URL.");

        if (response.Content.Headers.ContentLength is > 0 and var length && length > maximumBytes)
            throw new InvalidDataException("GitHub response exceeds the allowed size.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream(Math.Min(maximumBytes, (int)(response.Content.Headers.ContentLength ?? 0)));
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException("GitHub response exceeds the allowed size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(output.ToArray());
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("GitHub response is not valid UTF-8.", ex);
        }
    }
}
