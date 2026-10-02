using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace OneRugbyNavi2;

public static partial class TeamIndexParser
{
    [GeneratedRegex(@"^/team/(?<id>\d+)/?$", RegexOptions.CultureInvariant)]
    private static partial Regex TeamPathRegex();

    public static TeamIndexResult Parse(string? html, int expectedSeasonStartYear, string sourceUrl)
    {
        var detectedYear = LeagueOneSeasonDetector.Detect(html);
        if (string.IsNullOrWhiteSpace(html) || detectedYear == 0 || detectedYear != expectedSeasonStartYear)
        {
            return new TeamIndexResult(SyncReadiness.NotPublished, detectedYear, Array.Empty<TeamIndexEntry>());
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
        {
            return new TeamIndexResult(SyncReadiness.Failed, detectedYear, Array.Empty<TeamIndexEntry>());
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var entries = new List<TeamIndexEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sections = document.DocumentNode.SelectNodes("//section[starts-with(@id, 'division-')]");
        if (sections == null)
        {
            return new TeamIndexResult(SyncReadiness.Partial, detectedYear, entries);
        }

        foreach (var section in sections)
        {
            var divisionId = section.GetAttributeValue("id", "");
            var divisionMatch = Regex.Match(divisionId, @"^division-(?<number>[123])$", RegexOptions.CultureInvariant);
            var divisionHeader = Clean(section.SelectSingleNode(".//h2")?.InnerText ?? "");
            if (!divisionMatch.Success || !divisionHeader.Contains(divisionMatch.Groups["number"].Value, StringComparison.Ordinal))
            {
                continue;
            }

            var divisionCode = $"DIV{divisionMatch.Groups["number"].Value}";
            var anchors = section.SelectNodes(".//a[@href]");
            if (anchors == null) continue;

            foreach (var anchor in anchors)
            {
                var href = Clean(anchor.GetAttributeValue("href", ""));
                if (!Uri.TryCreate(sourceUri, href, out var teamUri)) continue;
                var pathMatch = TeamPathRegex().Match(teamUri.AbsolutePath);
                if (!pathMatch.Success || teamUri.Host != sourceUri.Host) continue;

                var name = Clean(anchor.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' name ')]")?.InnerText
                    ?? anchor.SelectSingleNode(".//img")?.GetAttributeValue("alt", "")
                    ?? "");
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(pathMatch.Groups["id"].Value)) continue;

                var imageUrl = anchor.SelectSingleNode(".//img")?.GetAttributeValue("src", "") ?? "";
                entries.Add(new TeamIndexEntry(
                    pathMatch.Groups["id"].Value,
                    name,
                    divisionCode,
                    teamUri.AbsoluteUri,
                    ResolveUrl(sourceUri, imageUrl)));
            }
        }

        return new TeamIndexResult(
            entries.Count > 0 ? SyncReadiness.Ready : SyncReadiness.Partial,
            detectedYear,
            entries);
    }

    private static string ResolveUrl(Uri baseUri, string value)
        => Uri.TryCreate(baseUri, value, out var result) ? result.AbsoluteUri : "";

    private static string Clean(string value)
        => Regex.Replace(HtmlEntity.DeEntitize(value ?? ""), @"\s+", " ").Trim();
}
