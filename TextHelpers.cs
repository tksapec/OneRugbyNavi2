using System.Globalization;
using System.Text;

namespace OneRugbyNavi2;

public static class SearchNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (char.IsWhiteSpace(c) || c == '・' || c == '･')
            {
                continue;
            }

            builder.Append(char.ToUpper(c, CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

public static class Initials
{
    public static string FromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var text = value.Trim();
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune))
            {
                builder.Append(rune.ToString());
            }

            if (builder.Length >= 2)
            {
                break;
            }
        }

        return builder.Length == 0 ? "?" : builder.ToString();
    }
}
