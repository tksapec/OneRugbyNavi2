using System.Text;

namespace OneRugbyNavi2;

public sealed class PlayerCatalogLoader
{
    public const string CatalogFileName = "league-one-players-2026-27.json";

    public async Task<PlayerCatalogDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(CatalogFileName);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var json = await reader.ReadToEndAsync(cancellationToken);
        if (!PlayerCatalogParser.TryParse(json, out var catalog, out var errors))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        return catalog;
    }
}
