namespace OneRugbyNavi2;

public static class TeamClubPageLink
{
    public static bool TryCreateUri(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            !parsed.IsDefaultPort ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            parsed.Host.Equals("league-one.jp", StringComparison.OrdinalIgnoreCase) ||
            parsed.Host.EndsWith(".league-one.jp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
