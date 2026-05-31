namespace OneRugbyNavi2;

public static class AssetImageResolver
{
    public static ImageSource? CreateImageSource(string? localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return null;
        }

        var normalized = localPath.Replace('\\', '/').TrimStart('/');
        var appDataPath = Path.Combine(LeagueOneDatabase.AppDataRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(appDataPath))
        {
            return ImageSource.FromFile(appDataPath);
        }

        return new StreamImageSource
        {
            Stream = cancellationToken => FileSystem.OpenAppPackageFileAsync(normalized)
        };
    }
}
