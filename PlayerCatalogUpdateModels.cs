using System.Text.Json;

namespace OneRugbyNavi2;

public sealed class PlayerCatalogManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public int FormatVersion { get; init; }
    public string CatalogPath { get; init; } = "";

    public bool TryGetPackageAssetPath(out string assetPath)
    {
        assetPath = "";
        if (FormatVersion != 1 || !IsSafeCatalogPath(CatalogPath))
            return false;

        assetPath = CatalogPath["Resources/Raw/".Length..];
        return true;
    }

    public static bool TryParse(string? json, out PlayerCatalogManifest manifest, out string error)
    {
        manifest = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Manifest JSON is empty.";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PlayerCatalogManifest>(json, JsonOptions);
            if (parsed is null)
            {
                error = "Manifest JSON has no document.";
                return false;
            }

            if (parsed.FormatVersion != 1)
            {
                error = $"Unsupported manifest format version: {parsed.FormatVersion}.";
                return false;
            }

            if (!IsSafeCatalogPath(parsed.CatalogPath))
            {
                error = "Catalog path must be a relative JSON path under Resources/Raw/.";
                return false;
            }

            manifest = parsed;
            error = "";
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid manifest JSON: {ex.Message}";
            return false;
        }
    }

    private static bool IsSafeCatalogPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Resources/Raw/", StringComparison.Ordinal))
            return false;
        if (path.Contains('\\') || path.Contains(':') || path.Contains('?') || path.Contains('#') || path.Contains('%'))
            return false;

        var segments = path.Split('/');
        return segments.Length >= 3 &&
               segments[0] == "Resources" && segments[1] == "Raw" &&
               segments.Skip(2).All(segment => segment.Length > 0 && segment is not ("." or "..")) &&
               Path.GetExtension(path).Equals(".json", StringComparison.Ordinal);
    }
}

public sealed record PlayerCatalogUpdateResult(
    PlayerCatalogDocument Catalog,
    DateTimeOffset CheckedAtUtc,
    bool Changed,
    bool UsedBundledManifest = false);
