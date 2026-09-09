using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace OneRugbyNavi2;

public static partial class LeagueOneSeasonDetector
{
    [GeneratedRegex(@"(?<!\d)(?<start>20\d{2})\s*[-ｰ‐‑–—−]\s*(?<end>\d{2}|20\d{2})\s*シーズン", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();

    public static int Detect(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return 0;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var text = Normalize(document.DocumentNode.InnerText);
        var match = SeasonRegex().Match(text);
        if (!match.Success || !int.TryParse(match.Groups["start"].Value, out var startYear))
        {
            return 0;
        }

        var expectedEnd = (startYear + 1) % 100;
        var endText = match.Groups["end"].Value;
        if (!int.TryParse(endText, out var parsedEnd))
        {
            return 0;
        }

        var normalizedEnd = parsedEnd >= 2000 ? parsedEnd % 100 : parsedEnd;
        return normalizedEnd == expectedEnd ? startYear : 0;
    }

    private static string Normalize(string value)
        => Regex.Replace(HtmlEntity.DeEntitize(value ?? ""), @"\s+", " ").Trim();
}
