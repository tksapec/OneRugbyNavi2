using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace OneRugbyNavi2
{
    public static class ScheduleFetcher
    {
        public const int SeasonYear = 2025;
        private const string ScheduleBaseUrl = "https://league-one.jp/schedule/";

        public sealed class SeasonOption
        {
            public string SeasonKey { get; set; } = "";
            public string SeasonLabel { get; set; } = "";
        }

        public sealed class BroadcastLink
        {
            public string Text { get; set; } = "";
            public string Url { get; set; } = "";
        }

        public sealed class Item
        {
            public string SeasonKey { get; set; } = "";
            public string SeasonLabel { get; set; } = "";
            public int SeasonStartYear { get; set; } = SeasonYear;
            public string CategoryCode { get; set; } = "";
            public string CategoryLabel { get; set; } = "";
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
            public string MatchId { get; set; } = "";
            public string MatchCode { get; set; } = "";
            public string MatchInfoUrl { get; set; } = "";
            public string PreviewUrl { get; set; } = "";
            public string ReportUrl { get; set; } = "";
            public string BroadcastText { get; set; } = "";
            public List<BroadcastLink> BroadcastLinks { get; set; } = new();
        }

        public sealed class FetchAllResult
        {
            public List<SeasonOption> Seasons { get; init; } = new();
            public string SeasonKey { get; init; } = "";
            public string SeasonLabel { get; init; } = "";
            public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.Now;
            public List<Item> Div1 { get; init; } = new();
            public List<Item> Div2 { get; init; } = new();
            public List<Item> Div3 { get; init; } = new();
            public List<Item> Replacement { get; init; } = new();
            public List<Item> Other { get; init; } = new();
            public string? Div1Error { get; init; }
            public string? Div2Error { get; init; }
            public string? Div3Error { get; init; }
            public string? ReplacementError { get; init; }
            public string? OtherError { get; init; }
            public string? ResultsError { get; init; }

            public bool HasAnyData =>
                Div1.Count > 0 ||
                Div2.Count > 0 ||
                Div3.Count > 0 ||
                Replacement.Count > 0 ||
                Other.Count > 0;

            public IEnumerable<string> GetErrors()
            {
                if (!string.IsNullOrWhiteSpace(Div1Error)) yield return $"D1: {Div1Error}";
                if (!string.IsNullOrWhiteSpace(Div2Error)) yield return $"D2: {Div2Error}";
                if (!string.IsNullOrWhiteSpace(Div3Error)) yield return $"D3: {Div3Error}";
                if (!string.IsNullOrWhiteSpace(ReplacementError)) yield return $"入替戦: {ReplacementError}";
                if (!string.IsNullOrWhiteSpace(OtherError)) yield return $"その他: {OtherError}";
                if (!string.IsNullOrWhiteSpace(ResultsError)) yield return $"Schedule: {ResultsError}";
            }
        }

        private sealed class CategoryDefinition
        {
            public string TabId { get; init; } = "";
            public string Code { get; init; } = "";
            public string Label { get; init; } = "";
        }

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static Task<FetchAllResult> FetchAllAsync() => FetchSeasonAsync(null);

        public static async Task<FetchAllResult> FetchSeasonAsync(string? seasonKey)
        {
            var url = string.IsNullOrWhiteSpace(seasonKey)
                ? ScheduleBaseUrl
                : $"{ScheduleBaseUrl}?year={Uri.EscapeDataString(seasonKey)}";
            var html = await Http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var seasons = ParseSeasons(doc);
            var selectedSeason = SelectSeason(seasons, seasonKey);
            var categories = ParseCategories(doc);

            var div1 = new List<Item>();
            var div2 = new List<Item>();
            var div3 = new List<Item>();
            var replacement = new List<Item>();
            var other = new List<Item>();

            foreach (var category in categories)
            {
                var items = ParseCategory(doc, category, selectedSeason);
                switch (category.Code)
                {
                    case "D1":
                        div1.AddRange(items);
                        break;
                    case "D2":
                        div2.AddRange(items);
                        break;
                    case "D3":
                        div3.AddRange(items);
                        break;
                    case "Replacement":
                        replacement.AddRange(items);
                        break;
                    default:
                        other.AddRange(items);
                        break;
                }
            }

#if DEBUG
            Debug.WriteLine($"Schedule page rows D1={div1.Count}, D2={div2.Count}, D3={div3.Count}, Replacement={replacement.Count}, Other={other.Count}");
#endif

            return new FetchAllResult
            {
                Seasons = seasons,
                SeasonKey = selectedSeason.SeasonKey,
                SeasonLabel = selectedSeason.SeasonLabel,
                FetchedAt = DateTimeOffset.Now,
                Div1 = div1,
                Div2 = div2,
                Div3 = div3,
                Replacement = replacement,
                Other = other,
                ResultsError = div1.Count + div2.Count + div3.Count + replacement.Count + other.Count == 0
                    ? "No schedule cards were found on the official schedule page. The HTML structure may have changed, or parsing may have failed."
                    : null
            };
        }

        private static List<SeasonOption> ParseSeasons(HtmlDocument doc)
        {
            var options = doc.DocumentNode.SelectNodes("//select[@id='season-select']/option");
            if (options == null)
            {
                return new List<SeasonOption>();
            }

            return options
                .Select(option => new SeasonOption
                {
                    SeasonKey = Clean(option.GetAttributeValue("value", "")),
                    SeasonLabel = Clean(option.InnerText)
                })
                .Where(option => !string.IsNullOrWhiteSpace(option.SeasonKey) && !string.IsNullOrWhiteSpace(option.SeasonLabel))
                .ToList();
        }

        private static SeasonOption SelectSeason(IReadOnlyList<SeasonOption> seasons, string? requestedKey)
        {
            var selected = !string.IsNullOrWhiteSpace(requestedKey)
                ? seasons.FirstOrDefault(season => string.Equals(season.SeasonKey, requestedKey, StringComparison.Ordinal))
                : seasons.FirstOrDefault();

            if (selected != null)
            {
                return selected;
            }

            var key = string.IsNullOrWhiteSpace(requestedKey) ? SeasonYear.ToString() : requestedKey.Trim();
            return new SeasonOption
            {
                SeasonKey = key,
                SeasonLabel = ToSeasonLabel(key)
            };
        }

        private static List<CategoryDefinition> ParseCategories(HtmlDocument doc)
        {
            var links = doc.DocumentNode.SelectNodes("//div[contains(@class,'tab-content-wrap')]//ul[contains(@class,'tab-links')]//a[@href]");
            var categories = new List<CategoryDefinition>();
            if (links != null)
            {
                foreach (var link in links)
                {
                    var href = link.GetAttributeValue("href", "");
                    var hashIndex = href.IndexOf('#');
                    var tabId = hashIndex >= 0 ? href[(hashIndex + 1)..] : "";
                    if (string.IsNullOrWhiteSpace(tabId))
                    {
                        continue;
                    }

                    var text = Clean(link.InnerText);
                    var (code, label) = NormalizeCategory(text, categories.Count + 1);
                    categories.Add(new CategoryDefinition
                    {
                        TabId = tabId,
                        Code = code,
                        Label = label
                    });
                }
            }

            if (categories.Count > 0)
            {
                return categories;
            }

            return new List<CategoryDefinition>
            {
                new() { TabId = "tab1", Code = "D1", Label = "D1" },
                new() { TabId = "tab2", Code = "D2", Label = "D2" },
                new() { TabId = "tab3", Code = "D3", Label = "D3" },
                new() { TabId = "tab4", Code = "Replacement", Label = "入替戦" }
            };
        }

        private static (string Code, string Label) NormalizeCategory(string value, int index)
        {
            var text = NormalizeSpaces(value);
            if (text.Contains("DIVISION 1", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"\bD1\b", RegexOptions.IgnoreCase))
            {
                return ("D1", "D1");
            }

            if (text.Contains("DIVISION 2", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"\bD2\b", RegexOptions.IgnoreCase))
            {
                return ("D2", "D2");
            }

            if (text.Contains("DIVISION 3", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"\bD3\b", RegexOptions.IgnoreCase))
            {
                return ("D3", "D3");
            }

            if (text.Contains("入替", StringComparison.Ordinal))
            {
                return ("Replacement", "入替戦");
            }

            return ($"Other{index}", string.IsNullOrWhiteSpace(text) ? "その他" : text);
        }

        private static List<Item> ParseCategory(HtmlDocument doc, CategoryDefinition category, SeasonOption season)
        {
            var tab = doc.DocumentNode.SelectSingleNode($"//div[@id='{category.TabId}']");
            var cards = tab?.SelectNodes(".//div[contains(concat(' ', normalize-space(@class), ' '), ' c-schedule ')]");
            var list = new List<Item>();
            if (cards == null)
            {
                return list;
            }

            foreach (var card in cards)
            {
                var item = ParseScheduleCard(card, category, season);
                if (item != null)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private static Item? ParseScheduleCard(HtmlNode card, CategoryDefinition category, SeasonOption season)
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

            var infoAnchor = card.SelectSingleNode(".//a[contains(@class,'btn-match-detail')]");
            var previewAnchor = card.SelectSingleNode(".//a[contains(@href,'/match_previews/')]");
            var reportAnchor = card.SelectSingleNode(".//a[contains(@href,'/match_reports/')]");
            var matchInfoUrl = ToAbsoluteUrl(infoAnchor?.GetAttributeValue("href", ""));
            var matchCode = ParseMatchCode(titleText);
            var homeScore = ParseNullableScore(Clean(homeNode.SelectSingleNode(".//p[contains(@class,'score')]")?.InnerText));
            var awayScore = ParseNullableScore(Clean(awayNode.SelectSingleNode(".//p[contains(@class,'score')]")?.InnerText));
            var (pref, venue) = SplitPlace(placeText);
            var broadcastLinks = ParseBroadcastLinks(card);

            return new Item
            {
                SeasonKey = season.SeasonKey,
                SeasonLabel = season.SeasonLabel,
                SeasonStartYear = ParseSeasonStartYear(season.SeasonKey),
                CategoryCode = category.Code,
                CategoryLabel = category.Label,
                Division = category.Label,
                Round = ParseRoundFromTitle(titleText),
                Date = MergeDate(ToJapaneseDate(dateText), BracketDow(dow)),
                Kickoff = kickoff,
                Conference = ParseConferenceFromTitle(titleText),
                Home = GetTeamName(homeNode),
                Away = GetTeamName(awayNode),
                Pref = pref,
                Venue = venue,
                HomeScore = homeScore,
                AwayScore = awayScore,
                Status = ParseStatus(Clean(infoAnchor?.InnerText), kickoff, homeScore, awayScore),
                MatchId = ParseMatchId(matchInfoUrl),
                MatchCode = matchCode,
                MatchInfoUrl = matchInfoUrl,
                PreviewUrl = ToAbsoluteUrl(previewAnchor?.GetAttributeValue("href", "")),
                ReportUrl = ToAbsoluteUrl(reportAnchor?.GetAttributeValue("href", "")),
                BroadcastText = string.Join(" / ", broadcastLinks.Select(link => link.Text).Where(text => !string.IsNullOrWhiteSpace(text)).Distinct()),
                BroadcastLinks = broadcastLinks
            };
        }

        private static List<BroadcastLink> ParseBroadcastLinks(HtmlNode card)
        {
            var anchors = card.SelectNodes(".//dl[contains(@class,'broadcast')]//a[@href]");
            if (anchors == null)
            {
                return new List<BroadcastLink>();
            }

            return anchors
                .Select(anchor => new BroadcastLink
                {
                    Text = Clean(anchor.InnerText),
                    Url = ToAbsoluteUrl(anchor.GetAttributeValue("href", ""))
                })
                .Where(link => !string.IsNullOrWhiteSpace(link.Text))
                .ToList();
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
            foreach (var pattern in new[]
            {
                @"第\d+節",
                @"第[１２12一二]戦",
                @"準々決勝",
                @"準決勝",
                @"3位決定戦／決勝",
                @"3位決定戦",
                @"決勝",
                @"プレーオフトーナメント"
            })
            {
                var match = Regex.Match(title, pattern);
                if (match.Success)
                {
                    return match.Value;
                }
            }

            return ParseMatchCode(title);
        }

        private static string ParseConferenceFromTitle(string title)
        {
            var match = Regex.Match(
                title,
                @"(カンファレンス[AB]|交流戦|準々決勝|準決勝|3位決定戦／決勝|3位決定戦|決勝|D1/D2入替戦|D2/D3入替戦|D1\s*\d+位vsD2\s*\d+位|D2\s*\d+位vsD3\s*\d+位)");
            return match.Success ? Clean(match.Groups[1].Value) : "";
        }

        private static string ParseMatchCode(string title)
        {
            var match = Regex.Match(title, @"\((?<code>[^()]+)\)\s*$");
            return match.Success ? Clean(match.Groups["code"].Value) : "";
        }

        private static string ParseMatchId(string url)
        {
            var match = Regex.Match(url, @"/match/(?<id>\d+)");
            return match.Success ? match.Groups["id"].Value : "";
        }

        private static string ParseStatus(string infoText, string kickoff, int? homeScore, int? awayScore)
        {
            var match = Regex.Match(infoText, @"\((.+)\)");
            if (match.Success)
            {
                return Clean(match.Groups[1].Value);
            }

            if (string.Equals(Clean(kickoff), "未定", StringComparison.Ordinal))
            {
                return "未定";
            }

            if (homeScore.HasValue && awayScore.HasValue)
            {
                return "試合終了";
            }

            return "試合前";
        }

        private static int? ParseNullableScore(string text)
        {
            var cleaned = Clean(text).Replace(" ", "", StringComparison.Ordinal);
            return int.TryParse(cleaned, out var score) ? score : null;
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

            return $"{int.Parse(match.Groups["month"].Value)}月{int.Parse(match.Groups["day"].Value)}日";
        }

        private static int ParseSeasonStartYear(string seasonKey)
        {
            return int.TryParse(seasonKey, out var year) ? year : SeasonYear;
        }

        private static string ToSeasonLabel(string seasonKey)
        {
            if (!int.TryParse(seasonKey, out var year))
            {
                return seasonKey;
            }

            return year == 2021 ? "2022シーズン" : $"{year}-{(year + 1) % 100:00}シーズン";
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
            if (dow.Contains("(") || dow.Contains(")") || dow.Contains("（") || dow.Contains("）")) return dow;
            return $"({dow})";
        }

        private static string Clean(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            return NormalizeSpaces(HtmlEntity.DeEntitize(s)
                .Replace("\u00A0", " ", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal));
        }

        private static string NormalizeSpaces(string value)
        {
            return Regex.Replace(value, @"\s+", " ").Trim();
        }
    }
}
