using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace OneRugbyNavi2;

public static partial class TeamCatalogParser
{
    [GeneratedRegex(@"^DIVISION\s*([123])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DivisionRegex();

    public static IReadOnlyList<TeamCatalogEntry> Parse(string? html, int seasonStartYear, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<TeamCatalogEntry>();
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var results = new List<TeamCatalogEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var currentDivision = "";

        foreach (var node in document.DocumentNode.DescendantsAndSelf())
        {
            if (node.NodeType != HtmlNodeType.Element)
            {
                continue;
            }

            var ownText = NormalizeOwnText(node);
            var divisionMatch = DivisionRegex().Match(ownText);
            if (divisionMatch.Success)
            {
                currentDivision = $"DIV{divisionMatch.Groups[1].Value}";
                continue;
            }

            if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(currentDivision))
            {
                var teamName = NormalizeTeamLinkText(node.InnerText);
                if (string.IsNullOrWhiteSpace(teamName))
                {
                    continue;
                }

                var key = $"{currentDivision}\u001f{teamName}";
                if (seen.Add(key))
                {
                    results.Add(new TeamCatalogEntry(teamName, currentDivision));
                }
            }
        }

        return results;
    }

    private static string NormalizeOwnText(HtmlNode node)
    {
        var pieces = node.ChildNodes
            .Where(child => child.NodeType == HtmlNodeType.Text)
            .Select(child => child.InnerText);
        return Normalize(string.Join(" ", pieces));
    }

    private static string NormalizeTeamLinkText(string? value)
    {
        var text = Normalize(value ?? "");
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        text = Regex.Replace(text, @"\s*公式サイト\s*$", "", RegexOptions.CultureInvariant).Trim();
        if (text.Length < 2 || text is "チケット" or "スケジュール" or "選手一覧" or "プロフィール")
        {
            return "";
        }

        return text;
    }

    private static string Normalize(string value)
        => Regex.Replace(HtmlEntity.DeEntitize(value), @"\s+", " ").Trim();
}
