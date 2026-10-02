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

        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
        {
            var sections = document.DocumentNode.SelectNodes("//section[starts-with(@id, 'division-')]");
            if (sections != null)
            {
                var sectionResults = new List<TeamCatalogEntry>();
                var sectionSeen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var section in sections)
                {
                    var match = Regex.Match(section.GetAttributeValue("id", ""), @"^division-([123])$", RegexOptions.CultureInvariant);
                    if (!match.Success) continue;
                    var division = $"DIV{match.Groups[1].Value}";
                    var anchors = section.SelectNodes(".//a[@href]");
                    if (anchors == null) continue;
                    foreach (var anchor in anchors)
                    {
                        var name = NormalizeTeamLinkText(anchor.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' name ')]")?.InnerText
                            ?? anchor.SelectSingleNode(".//img")?.GetAttributeValue("alt", "")
                            ?? anchor.InnerText);
                        if (string.IsNullOrWhiteSpace(name) || !sectionSeen.Add($"{division}\u001f{name}")) continue;
                        var image = anchor.SelectSingleNode(".//img")?.GetAttributeValue("src", "") ?? "";
                        sectionResults.Add(new TeamCatalogEntry(
                            name,
                            division,
                            LogoUrl: Uri.TryCreate(sourceUri, image, out var logoUri) ? logoUri.AbsoluteUri : ""));
                    }
                }

                return sectionResults;
            }
        }

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
