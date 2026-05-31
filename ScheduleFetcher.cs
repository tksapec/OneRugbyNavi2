using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace OneRugbyNavi2
{
    public static class ScheduleFetcher
    {
        public const int SeasonYear = 2025;

        public sealed class Item
        {
            public string Division { get; set; } = "";
            public string Round { get; set; } = "";
            public string Date { get; set; } = "";
            public string Kickoff { get; set; } = "";
            public string Conference { get; set; } = "";
            public string Home { get; set; } = "";
            public string Away { get; set; } = "";
            public string Pref { get; set; } = "";
            public string Venue { get; set; } = "";
            public int? HomeScore { get; set; }
            public int? AwayScore { get; set; }
            public string Status { get; set; } = "";
            public string MatchInfoUrl { get; set; } = "";
            public string ReportUrl { get; set; } = "";
        }

        public sealed class FetchAllResult
        {
            public List<Item> Div1 { get; init; } = new();
            public List<Item> Div2 { get; init; } = new();
            public List<Item> Div3 { get; init; } = new();
            public string? Div1Error { get; init; }
            public string? Div2Error { get; init; }
            public string? Div3Error { get; init; }
            public string? ResultsError { get; init; }

            public bool HasAnyData => Div1.Count > 0 || Div2.Count > 0 || Div3.Count > 0;

            public IEnumerable<string> GetErrors()
            {
                if (!string.IsNullOrWhiteSpace(Div1Error)) yield return $"Div1: {Div1Error}";
                if (!string.IsNullOrWhiteSpace(Div2Error)) yield return $"Div2: {Div2Error}";
                if (!string.IsNullOrWhiteSpace(Div3Error)) yield return $"Div3: {Div3Error}";
                if (!string.IsNullOrWhiteSpace(ResultsError)) yield return $"Results: {ResultsError}";
            }
        }

        private sealed class DivisionFetchOutcome
        {
            public List<Item> Items { get; init; } = new();
            public string? Error { get; init; }
        }

        private static readonly HttpClient http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static async Task<FetchAllResult> FetchAllAsync()
        {
            var d1Task = SafeFetchDivisionAsync(
                $"https://league-one.jp/content/schedule_table/{SeasonYear}/div1",
                "DIV1");
            var d2Task = SafeFetchDivisionAsync(
                $"https://league-one.jp/content/schedule_table/{SeasonYear}/div2",
                "DIV2");
            var d3Task = SafeFetchDivisionAsync(
                $"https://league-one.jp/content/schedule_table/{SeasonYear}/div3",
                "DIV3");
            var resultPageTask = SafeFetchResultsPageAsync(SeasonYear);

            await Task.WhenAll(d1Task, d2Task, d3Task, resultPageTask);

            var d1 = await d1Task;
            var d2 = await d2Task;
            var d3 = await d3Task;
            var resultPage = await resultPageTask;

            MergeResultData(d1.Items, resultPage.Div1);
            MergeResultData(d2.Items, resultPage.Div2);
            MergeResultData(d3.Items, resultPage.Div3);

            return new FetchAllResult
            {
                Div1 = d1.Items,
                Div2 = d2.Items,
                Div3 = d3.Items,
                Div1Error = d1.Error,
                Div2Error = d2.Error,
                Div3Error = d3.Error,
                ResultsError = resultPage.Error
            };
        }

        private static async Task<DivisionFetchOutcome> SafeFetchDivisionAsync(string url, string divisionName)
        {
            try
            {
                var items = await FetchDivisionAsync(url, divisionName);
#if DEBUG
                Debug.WriteLine($"{divisionName} schedule rows: {items.Count}");
#endif
                return new DivisionFetchOutcome
                {
                    Items = items,
                    Error = items.Count == 0
                        ? "No schedule rows were found. The HTML structure may have changed, or parsing may have failed."
                        : null
                };
            }
            catch (Exception ex)
            {
                return new DivisionFetchOutcome
                {
                    Error = ex.Message
                };
            }
        }

        private static async Task<(List<Item> Div1, List<Item> Div2, List<Item> Div3, string? Error)> SafeFetchResultsPageAsync(int year)
        {
            try
            {
                return await FetchResultsPageAsync(year);
            }
            catch (Exception ex)
            {
                return (new List<Item>(), new List<Item>(), new List<Item>(), ex.Message);
            }
        }

        private static async Task<List<Item>> FetchDivisionAsync(string url, string divisionName)
        {
            var html = await http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var list = new List<Item>();
            var rows = doc.DocumentNode.SelectNodes("//table[contains(@class,'schedule-table')]//tbody/tr");
            if (rows == null)
            {
                return list;
            }

            string lastRound = "";

            foreach (var row in rows)
            {
                var tds = row.SelectNodes("./td");
                if (tds == null || tds.Count == 0)
                {
                    continue;
                }

                bool isDiv1 = divisionName == "DIV1";
                bool hasRoundColumn = tds.Count >= (isDiv1 ? 8 : 7);
                int offset = hasRoundColumn ? 0 : -1;

                string round = hasRoundColumn ? GetCellText(tds, 0) : lastRound;
                string date = GetCellText(tds, 1 + offset);
                string dow = GetCellText(tds, 2 + offset);
                string conference = isDiv1 ? GetCellText(tds, 3 + offset) : "";
                string kickoff = GetCellText(tds, (isDiv1 ? 4 : 3) + offset);
                var teamsCell = GetCell(tds, (isDiv1 ? 5 : 4) + offset);
                string pref = GetCellText(tds, (isDiv1 ? 6 : 5) + offset);
                string venue = GetCellText(tds, (isDiv1 ? 7 : 6) + offset);

                if (string.IsNullOrWhiteSpace(round))
                {
                    round = lastRound;
                }

                var (home, away) = ParseTeams(teamsCell);
                list.Add(new Item
                {
                    Division = divisionName,
                    Round = round,
                    Date = MergeDate(date, BracketDow(dow)),
                    Conference = conference,
                    Kickoff = kickoff,
                    Home = home,
                    Away = away,
                    Pref = pref,
                    Venue = venue
                });

                if (!string.IsNullOrWhiteSpace(round))
                {
                    lastRound = round;
                }
            }

            return list;
        }

        private static async Task<(List<Item> Div1, List<Item> Div2, List<Item> Div3, string? Error)> FetchResultsPageAsync(int year)
        {
            var html = await http.GetStringAsync($"https://league-one.jp/schedule/?year={year}");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var div1 = ParseResultsDivision(doc, "tab1", "DIV1");
            var div2 = ParseResultsDivision(doc, "tab2", "DIV2");
            var div3 = ParseResultsDivision(doc, "tab3", "DIV3");
            var totalCount = div1.Count + div2.Count + div3.Count;
#if DEBUG
            Debug.WriteLine($"DIV1 result cards: {div1.Count}");
            Debug.WriteLine($"DIV2 result cards: {div2.Count}");
            Debug.WriteLine($"DIV3 result cards: {div3.Count}");
#endif

            return (
                div1,
                div2,
                div3,
                totalCount == 0
                    ? "No result cards were found on the results page. The HTML structure may have changed, or parsing may have failed."
                    : null);
        }

        private static List<Item> ParseResultsDivision(HtmlDocument doc, string tabId, string divisionName)
        {
            var tab = doc.DocumentNode.SelectSingleNode($"//div[@id='{tabId}']");
            var list = new List<Item>();
            if (tab == null)
            {
                return list;
            }

            var accordions = tab.SelectNodes(".//div[contains(@class,'c-accordion')]");
            if (accordions == null)
            {
                return list;
            }

            foreach (var accordion in accordions)
            {
                var round = Clean(accordion.SelectSingleNode("./div[contains(@class,'ttl-wrap')]//em")?.InnerText);
                var cards = accordion.SelectNodes(".//div[contains(@class,'slide-toggle-target')]//div[contains(@class,'c-schedule')]");
                if (cards == null)
                {
                    continue;
                }

                foreach (var card in cards)
                {
                    var item = ParseResultCard(card, divisionName, round);
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }

            return list;
        }

        private static Item? ParseResultCard(HtmlNode card, string divisionName, string round)
        {
            var titleText = Clean(card.SelectSingleNode(".//div[contains(@class,'ttl-wrap')]//h3")?.InnerText);
            var placeText = Clean(card.SelectSingleNode(".//p[contains(@class,'place')]")?.InnerText);
            var dateNode = card.SelectSingleNode(".//div[contains(@class,'datetime')]//p[contains(@class,'date')]");
            var dateText = ParseDotDate(dateNode);
            var dow = Clean(card.SelectSingleNode(".//div[contains(@class,'datetime')]//span[contains(@class,'youbi')]")?.InnerText);
            var kickoff = Clean(card.SelectSingleNode(".//div[contains(@class,'datetime')]//p[contains(@class,'time')]")?.InnerText);

            var homeNode = card.SelectSingleNode(".//ul[contains(concat(' ', normalize-space(@class), ' '), ' game ')]//li[contains(concat(' ', normalize-space(@class), ' '), ' home ')]");
            var awayNode = card.SelectSingleNode(".//ul[contains(concat(' ', normalize-space(@class), ' '), ' game ')]//li[contains(concat(' ', normalize-space(@class), ' '), ' away ')]");
            if (homeNode == null || awayNode == null)
            {
                return null;
            }

            string home = GetTeamName(homeNode);
            string away = GetTeamName(awayNode);
            int? homeScore = ParseNullableScore(Clean(homeNode.SelectSingleNode(".//p[contains(@class,'score')]")?.InnerText));
            int? awayScore = ParseNullableScore(Clean(awayNode.SelectSingleNode(".//p[contains(@class,'score')]")?.InnerText));

            var infoAnchor = card.SelectSingleNode(".//a[contains(@class,'btn-match-detail')]");
            var reportAnchor = card.SelectSingleNode(".//a[contains(@href,'/match_reports/')]");
            string status = ParseStatus(Clean(infoAnchor?.InnerText), kickoff, homeScore, awayScore);
            string conference = ParseConferenceFromTitle(titleText);
            var (pref, venue) = SplitPlace(placeText);

            return new Item
            {
                Division = divisionName,
                Round = string.IsNullOrWhiteSpace(round) ? ParseRoundFromTitle(titleText) : round,
                Date = MergeDate(ToJapaneseDate(dateText), BracketDow(dow)),
                Conference = conference,
                Kickoff = kickoff,
                Home = home,
                Away = away,
                Pref = pref,
                Venue = venue,
                HomeScore = homeScore,
                AwayScore = awayScore,
                Status = status,
                MatchInfoUrl = ToAbsoluteUrl(infoAnchor?.GetAttributeValue("href", "")),
                ReportUrl = ToAbsoluteUrl(reportAnchor?.GetAttributeValue("href", ""))
            };
        }

        private static void MergeResultData(List<Item> baseItems, List<Item> resultItems)
        {
            if (baseItems.Count == 0 || resultItems.Count == 0)
            {
                return;
            }
#if DEBUG
            int mergeCount = 0;
#endif

            var resultByRound = resultItems
                .GroupBy(item => item.Round)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var group in baseItems.GroupBy(item => item.Round))
            {
                if (!resultByRound.TryGetValue(group.Key, out var resultGroup) || resultGroup.Count == 0)
                {
                    continue;
                }

                var baseGroup = group.ToList();
                var used = new HashSet<Item>();
                var resultByMatchKey = resultGroup
                    .Select(item => new { Key = NormalizeMatchKey(item), Item = item })
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .GroupBy(pair => pair.Key)
                    .Where(keyGroup => keyGroup.Count() == 1)
                    .ToDictionary(keyGroup => keyGroup.Key, keyGroup => keyGroup.Single().Item);

                foreach (var baseItem in baseGroup)
                {
                    Item? match = null;
                    var baseKey = NormalizeMatchKey(baseItem);
                    if (!string.IsNullOrWhiteSpace(baseKey) &&
                        resultByMatchKey.TryGetValue(baseKey, out var exactMatch) &&
                        !used.Contains(exactMatch))
                    {
                        match = exactMatch;
                    }

                    match ??= resultGroup.FirstOrDefault(resultItem =>
                        !used.Contains(resultItem) &&
                        IsLikelySameMatch(baseItem, resultItem));

                    if (match == null)
                    {
                        continue;
                    }

                    used.Add(match);
                    CopyResultData(baseItem, match);
#if DEBUG
                    mergeCount++;
#endif
                }
            }
#if DEBUG
            Debug.WriteLine($"Result merge count: {mergeCount}");
#endif
        }

        private static bool IsLikelySameMatch(Item left, Item right)
        {
            if (!string.Equals(NormalizeDateKey(left.Date), NormalizeDateKey(right.Date), StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(NormalizeMatchText(left.Kickoff), NormalizeMatchText(right.Kickoff), StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(left.Conference) &&
                !string.IsNullOrWhiteSpace(right.Conference) &&
                !string.Equals(NormalizeConferenceKey(left.Conference), NormalizeConferenceKey(right.Conference), StringComparison.Ordinal))
            {
                return false;
            }

            if (HasCompleteTeamInfo(left, right))
            {
                return HasSameTeams(left, right);
            }

            return HasSameVenue(left, right);
        }

        private static string NormalizeMatchKey(Item item)
        {
            var parts = new[]
            {
                NormalizeDateKey(item.Date),
                NormalizeMatchText(item.Kickoff),
                NormalizeTeamKey(item.Home),
                NormalizeTeamKey(item.Away),
                NormalizeVenueKey(item)
            };

            return parts.Any(string.IsNullOrWhiteSpace) ? "" : string.Join("|", parts);
        }

        private static bool HasSameTeams(Item left, Item right)
        {
            var leftHome = NormalizeTeamKey(left.Home);
            var leftAway = NormalizeTeamKey(left.Away);
            var rightHome = NormalizeTeamKey(right.Home);
            var rightAway = NormalizeTeamKey(right.Away);

            return !string.IsNullOrWhiteSpace(leftHome) &&
                !string.IsNullOrWhiteSpace(leftAway) &&
                !string.IsNullOrWhiteSpace(rightHome) &&
                !string.IsNullOrWhiteSpace(rightAway) &&
                AreMatchNamesCompatible(leftHome, rightHome) &&
                AreMatchNamesCompatible(leftAway, rightAway);
        }

        private static bool HasCompleteTeamInfo(Item left, Item right)
        {
            return !string.IsNullOrWhiteSpace(NormalizeTeamKey(left.Home)) &&
                !string.IsNullOrWhiteSpace(NormalizeTeamKey(left.Away)) &&
                !string.IsNullOrWhiteSpace(NormalizeTeamKey(right.Home)) &&
                !string.IsNullOrWhiteSpace(NormalizeTeamKey(right.Away));
        }

        private static bool HasSameVenue(Item left, Item right)
        {
            var leftVenue = NormalizeVenueKey(left);
            var rightVenue = NormalizeVenueKey(right);

            return !string.IsNullOrWhiteSpace(leftVenue) &&
                !string.IsNullOrWhiteSpace(rightVenue) &&
                AreMatchNamesCompatible(leftVenue, rightVenue);
        }

        private static string NormalizeVenueKey(Item item)
        {
            return NormalizeMatchText(item.Venue);
        }

        private static string NormalizeConferenceKey(string value)
        {
            var key = NormalizeMatchText(value)
                .Replace("\u30AB\u30F3\u30D5\u30A1\u30EC\u30F3\u30B9", "", StringComparison.Ordinal)
                .Replace("\u6226", "", StringComparison.Ordinal);

            return key;
        }

        private static string NormalizeTeamKey(string value)
        {
            var key = NormalizeMatchText(value);
            foreach (var token in CorporateNameTokens)
            {
                key = key.Replace(token, "", StringComparison.Ordinal);
            }

            return key;
        }

        private static bool AreMatchNamesCompatible(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return true;
            }

            const int minimumPartialMatchLength = 4;
            return (left.Length >= minimumPartialMatchLength && right.Contains(left, StringComparison.Ordinal)) ||
                (right.Length >= minimumPartialMatchLength && left.Contains(right, StringComparison.Ordinal));
        }

        private static readonly string[] CorporateNameTokens =
        {
            "NEC",
            "NTT",
            "\u30EA\u30B3\u30FC",
            "\u30B5\u30F3\u30C8\u30EA\u30FC",
            "\u30D1\u30CA\u30BD\u30CB\u30C3\u30AF",
            "\u65E5\u672C\u88FD\u9244",
            "\u4E5D\u5DDE\u96FB\u529B",
            "\u6E05\u6C34\u5EFA\u8A2D",
            "\u8C4A\u7530\u81EA\u52D5\u7E54\u6A5F",
            "\u65E5\u91CE",
            "\u4E2D\u56FD\u96FB\u529B",
            "\u30AF\u30EA\u30BF",
            "\u30BB\u30B3\u30E0",
            "\u30E4\u30AF\u30EB\u30C8"
        };

        private static void CopyResultData(Item target, Item source)
        {
            target.HomeScore = source.HomeScore;
            target.AwayScore = source.AwayScore;
            target.Status = source.Status;
            target.MatchInfoUrl = source.MatchInfoUrl;
            target.ReportUrl = source.ReportUrl;
        }

        private static (string Home, string Away) ParseTeams(HtmlNode? teamsCell)
        {
            if (teamsCell == null)
            {
                return ("", "");
            }

            string home = CleanTeamText(teamsCell.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' home ')]")?.InnerText);
            string away = CleanTeamText(teamsCell.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' away ')]")?.InnerText);
            if (TrySplitVersus(home, out var splitHomeFromHome, out var splitAwayFromHome))
            {
                home = splitHomeFromHome;
                if (string.IsNullOrWhiteSpace(away))
                {
                    away = splitAwayFromHome;
                }
            }

            if (TrySplitVersus(away, out var splitHomeFromAway, out var splitAwayFromAway))
            {
                if (string.IsNullOrWhiteSpace(home))
                {
                    home = splitHomeFromAway;
                }

                away = splitAwayFromAway;
            }

            if (!string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(away))
            {
                return (home, away);
            }

            var names = teamsCell
                .SelectNodes(".//*[not(*)]")
                ?.Select(node => CleanTeamText(node.InnerText))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Where(IsLikelyTeamText)
                .ToList();

            if (names is { Count: >= 2 })
            {
                return (names[0], names[^1]);
            }

            var cellText = CleanTeamText(teamsCell.InnerText);
            if (TrySplitVersus(cellText, out var splitHome, out var splitAway))
            {
                return (splitHome, splitAway);
            }

            return ("", "");
        }

        private static bool TrySplitVersus(string text, out string home, out string away)
        {
            home = "";
            away = "";
            var match = Regex.Match(Clean(text), @"^(?<home>.+?)\s*(?<![A-Za-z])(?:v|V|vs|VS|Ｖ|ｖ)(?![A-Za-z])\s*(?<away>.+)$");
            if (!match.Success)
            {
                return false;
            }

            home = CleanTeamText(match.Groups["home"].Value);
            away = CleanTeamText(match.Groups["away"].Value);
            return !string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(away);
        }

        private static bool IsLikelyTeamText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                string.Equals(text, "V", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "VS", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(text, @"^\d+$"))
            {
                return false;
            }

            return !TrySplitVersus(text, out _, out _);
        }

        private static string CleanTeamText(string? text)
        {
            var cleaned = Clean(text);
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            return cleaned;
        }

        private static string GetTeamName(HtmlNode teamNode)
        {
            var preferred = teamNode.SelectSingleNode(".//p[contains(concat(' ', normalize-space(@class), ' '), ' name ') and contains(concat(' ', normalize-space(@class), ' '), ' only-pc ')]");
            if (preferred != null)
            {
                return Clean(preferred.InnerText);
            }

            return Clean(teamNode.SelectSingleNode(".//p[contains(concat(' ', normalize-space(@class), ' '), ' name ')]")?.InnerText);
        }

        private static string ParseRoundFromTitle(string title)
        {
            var match = Regex.Match(title, @"(\u7B2C\d+\u7BC0|PO\d+)");
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string ParseConferenceFromTitle(string title)
        {
            var match = Regex.Match(
                title,
                @"(\u30AB\u30F3\u30D5\u30A1\u30EC\u30F3\u30B9[AB]|\u4EA4\u6D41\u6226|\u6E96\u3005\u6C7A\u52DD\u2460|\u6E96\u3005\u6C7A\u52DD\u2461|\u6E96\u6C7A\u52DD\u2460|\u6E96\u6C7A\u52DD\u2461|3\u4F4D\u6C7A\u5B9A\u6226|\u6C7A\u52DD)");
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string ParseStatus(string infoText, string kickoff, int? homeScore, int? awayScore)
        {
            var match = Regex.Match(infoText, @"\((.+)\)");
            if (match.Success)
            {
                return Clean(match.Groups[1].Value);
            }

            if (string.Equals(Clean(kickoff), "\u672A\u5B9A", StringComparison.Ordinal))
            {
                return "\u672A\u5B9A";
            }

            if (homeScore.HasValue && awayScore.HasValue)
            {
                return "\u8A66\u5408\u7D42\u4E86";
            }

            return "\u8A66\u5408\u524D";
        }

        private static int? ParseNullableScore(string text)
        {
            return int.TryParse(text, out var score) ? score : null;
        }

        private static string ParseDotDate(HtmlNode? dateNode)
        {
            if (dateNode == null)
            {
                return "";
            }

            var match = Regex.Match(Clean(dateNode.InnerText), @"\d{1,2}\.\d{1,2}");
            return match.Success ? match.Value : "";
        }

        private static (string Pref, string Venue) SplitPlace(string placeText)
        {
            var match = Regex.Match(placeText, @"^(.*)\s+\((.+)\)$");
            if (!match.Success)
            {
                return ("", placeText);
            }

            return (Clean(match.Groups[2].Value), Clean(match.Groups[1].Value));
        }

        private static string ToJapaneseDate(string text)
        {
            var match = Regex.Match(text, @"(?<month>\d{1,2})\.(?<day>\d{1,2})");
            if (!match.Success)
            {
                return text;
            }

            return $"{int.Parse(match.Groups["month"].Value)}\u6708{int.Parse(match.Groups["day"].Value)}\u65E5";
        }

        private static string NormalizeDateKey(string value)
        {
            var match = Regex.Match(value, @"(?<month>\d{1,2})\u6708(?<day>\d{1,2})\u65E5");
            if (match.Success)
            {
                return $"{int.Parse(match.Groups["month"].Value):00}/{int.Parse(match.Groups["day"].Value):00}";
            }

            match = Regex.Match(value, @"(?<month>\d{1,2})\.(?<day>\d{1,2})");
            if (match.Success)
            {
                return $"{int.Parse(match.Groups["month"].Value):00}/{int.Parse(match.Groups["day"].Value):00}";
            }

            return Clean(value);
        }

        private static string NormalizeMatchText(string? value)
        {
            var text = Clean(value).Normalize(NormalizationForm.FormKC).ToUpperInvariant();
            return Regex.Replace(text, @"[\s\u3000・･/\-－―‐‑–—（）()\[\]【】「」『』,，.．]", "");
        }

        private static HtmlNode? GetCell(HtmlNodeCollection? tds, int index)
        {
            if (tds == null || index < 0 || index >= tds.Count)
            {
                return null;
            }

            return tds[index];
        }

        private static string GetCellText(HtmlNodeCollection? tds, int index)
        {
            return Clean(GetCell(tds, index)?.InnerText);
        }

        private static string ToAbsoluteUrl(string? href)
        {
            href = Clean(href);
            if (string.IsNullOrWhiteSpace(href))
            {
                return "";
            }

            if (href.StartsWith("//", StringComparison.Ordinal))
            {
                return $"https:{href}";
            }

            var baseUri = new Uri("https://league-one.jp/");

            if (href.StartsWith("/", StringComparison.Ordinal))
            {
                return new Uri(baseUri, href).ToString();
            }

            if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
            {
                return absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps
                    ? absolute.ToString()
                    : "";
            }

            return Uri.TryCreate(baseUri, href, out var relative)
                ? relative.ToString()
                : "";
        }

        private static string MergeDate(string date, string dowWithParen)
        {
            if (string.IsNullOrEmpty(date)) return dowWithParen;
            if (string.IsNullOrEmpty(dowWithParen)) return date;
            return $"{date}{dowWithParen}";
        }

        private static string BracketDow(string dow)
        {
            dow = Clean(dow);
            if (string.IsNullOrEmpty(dow)) return "";
            if (dow.Contains("(") || dow.Contains(")") || dow.Contains("\uFF08") || dow.Contains("\uFF09")) return dow;
            return $"({dow})";
        }

        private static string Clean(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            return HtmlEntity.DeEntitize(s)
                .Replace("\u00A0", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }
    }
}

