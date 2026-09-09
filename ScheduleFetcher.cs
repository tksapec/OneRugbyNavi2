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
            public int SeasonStartYear { get; set; }
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
            public string SourceUrl { get; set; } = "";
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

        private sealed class DivisionFetchResult
        {
            public string Division { get; init; } = "";
            public string SourceUrl { get; init; } = "";
            public List<Item> Items { get; init; } = new();
            public string? Error { get; init; }
        }

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static Task<FetchAllResult> FetchAllAsync() => FetchSeasonAsync(null);

        public static async Task<FetchAllResult> FetchSeasonAsync(string? seasonKey)
        {
            var requestedKey = string.IsNullOrWhiteSpace(seasonKey)
                ? SeasonCatalog.CurrentSeasonKey
                : seasonKey.Trim();
            var detailUrl = $"{ScheduleBaseUrl}?year={Uri.EscapeDataString(requestedKey)}";

            var seasons = new List<SeasonOption>();
            var detailedDiv1 = new List<Item>();
            var detailedDiv2 = new List<Item>();
            var detailedDiv3 = new List<Item>();
            var replacement = new List<Item>();
            var other = new List<Item>();
            string? detailError = null;

            try
            {
                var html = await Http.GetStringAsync(detailUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                seasons = ParseSeasons(doc);
                var selectedSeason = SelectSeason(seasons, requestedKey);
                var categories = ParseCategories(doc);

                foreach (var category in categories)
                {
                    var items = ParseCategory(doc, category, selectedSeason, detailUrl);
                    switch (category.Code)
                    {
                        case "D1":
                            detailedDiv1.AddRange(items);
                            break;
                        case "D2":
                            detailedDiv2.AddRange(items);
                            break;
                        case "D3":
                            detailedDiv3.AddRange(items);
                            break;
                        case "Replacement":
                            replacement.AddRange(items);
                            break;
                        default:
                            other.AddRange(items);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                detailError = ex.Message;
            }

            EnsureSeasonOption(seasons, requestedKey);
            var selected = SelectSeason(seasons, requestedKey);

            var tableTasks = new[]
            {
                FetchScheduleTableAsync(selected, "D1"),
                FetchScheduleTableAsync(selected, "D2"),
                FetchScheduleTableAsync(selected, "D3")
            };
            var tableResults = await Task.WhenAll(tableTasks);

            var tableDiv1 = tableResults.First(result => result.Division == "D1");
            var tableDiv2 = tableResults.First(result => result.Division == "D2");
            var tableDiv3 = tableResults.First(result => result.Division == "D3");

            var div1 = MergeRegularSeason(tableDiv1.Items, detailedDiv1);
            var div2 = MergeRegularSeason(tableDiv2.Items, detailedDiv2);
            var div3 = MergeRegularSeason(tableDiv3.Items, detailedDiv3);

#if DEBUG
            Debug.WriteLine($"Schedule rows season={selected.SeasonKey} D1={div1.Count}, D2={div2.Count}, D3={div3.Count}, Replacement={replacement.Count}, Other={other.Count}");
#endif

            var hasRegularData = div1.Count + div2.Count + div3.Count > 0;
            var hasAnyData = hasRegularData || replacement.Count + other.Count > 0;

            return new FetchAllResult
            {
                Seasons = seasons,
                SeasonKey = selected.SeasonKey,
                SeasonLabel = selected.SeasonLabel,
                FetchedAt = DateTimeOffset.Now,
                Div1 = div1,
                Div2 = div2,
                Div3 = div3,
                Replacement = Deduplicate(replacement),
                Other = Deduplicate(other),
                Div1Error = div1.Count == 0 ? tableDiv1.Error : null,
                Div2Error = div2.Count == 0 ? tableDiv2.Error : null,
                Div3Error = div3.Count == 0 ? tableDiv3.Error : null,
                ResultsError = !hasAnyData
                    ? detailError ?? "No schedule data was found on the official League One pages."
                    : null
            };
        }

        private static async Task<DivisionFetchResult> FetchScheduleTableAsync(SeasonOption season, string division)
        {
            var sourceUrl = SeasonCatalog.ScheduleTableUrl(season.SeasonKey, division.ToLowerInvariant());
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return new DivisionFetchResult
                {
                    Division = division,
                    SourceUrl = sourceUrl,
                    Error = "Invalid schedule-table URL."
                };
            }

            try
            {
                var html = await Http.GetStringAsync(sourceUrl);
                var items = ScheduleTableParser.Parse(html, season.SeasonKey, division, sourceUrl);
                return new DivisionFetchResult
                {
                    Division = division,
                    SourceUrl = sourceUrl,
                    Items = items,
                    Error = items.Count == 0 ? "No annual schedule rows were found." : null
                };
            }
            catch (Exception ex)
            {
                return new DivisionFetchResult
                {
                    Division = division,
                    SourceUrl = sourceUrl,
                    Error = ex.Message
                };
            }
        }

        private static List<Item> MergeRegularSeason(IEnumerable<Item> annualItems, IEnumerable<Item> detailItems)
        {
            var result = annualItems.ToList();
            foreach (var detail in detailItems)
            {
                NormalizeTeamNames(detail);
                var existing = result.FirstOrDefault(item => IsSameFixture(item, detail));
                if (existing == null)
                {
                    result.Add(detail);
                    continue;
                }

                ApplyLiveDetails(existing, detail);
            }

            return Deduplicate(result);
        }

        private static void NormalizeTeamNames(Item item)
        {
            item.Home = SeasonCatalog.NormalizeTeamName(item.Home, item.SeasonStartYear);
            item.Away = SeasonCatalog.NormalizeTeamName(item.Away, item.SeasonStartYear);
        }

        private static bool IsSameFixture(Item left, Item right)
        {
            if (!string.IsNullOrWhiteSpace(left.MatchId) &&
                !string.IsNullOrWhiteSpace(right.MatchId) &&
                string.Equals(left.MatchId, right.MatchId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(left.Home, right.Home, StringComparison.Ordinal) ||
                !string.Equals(left.Away, right.Away, StringComparison.Ordinal))
            {
                return false;
            }

            var leftDate = NormalizeDateKey(left.Date);
            var rightDate = NormalizeDateKey(right.Date);
            if (!string.IsNullOrWhiteSpace(leftDate) &&
                !string.IsNullOrWhiteSpace(rightDate) &&
                string.Equals(leftDate, rightDate, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(left.Round) &&
                   string.Equals(left.Round, right.Round, StringComparison.Ordinal);
        }

        private static void ApplyLiveDetails(Item target, Item detail)
        {
            if (ShouldUseIncomingDate(target.Date, detail.Date)) target.Date = detail.Date;
            if (!string.IsNullOrWhiteSpace(detail.Round)) target.Round = detail.Round;
            if (ShouldUseIncomingValue(target.Kickoff, detail.Kickoff)) target.Kickoff = detail.Kickoff;
            if (!string.IsNullOrWhiteSpace(detail.Conference)) target.Conference = detail.Conference;
            if (ShouldUseIncomingValue(target.Pref, detail.Pref)) target.Pref = detail.Pref;
            if (ShouldUseIncomingValue(target.Venue, detail.Venue)) target.Venue = detail.Venue;
            if (detail.HomeScore.HasValue) target.HomeScore = detail.HomeScore;
            if (detail.AwayScore.HasValue) target.AwayScore = detail.AwayScore;
            if (!string.IsNullOrWhiteSpace(detail.Status)) target.Status = detail.Status;
            if (!string.IsNullOrWhiteSpace(detail.MatchId)) target.MatchId = detail.MatchId;
            if (!string.IsNullOrWhiteSpace(detail.MatchCode)) target.MatchCode = detail.MatchCode;
            if (!string.IsNullOrWhiteSpace(detail.MatchInfoUrl)) target.MatchInfoUrl = detail.MatchInfoUrl;
            if (!string.IsNullOrWhiteSpace(detail.PreviewUrl)) target.PreviewUrl = detail.PreviewUrl;
            if (!string.IsNullOrWhiteSpace(detail.ReportUrl)) target.ReportUrl = detail.ReportUrl;
            if (!string.IsNullOrWhiteSpace(detail.BroadcastText)) target.BroadcastText = detail.BroadcastText;
            if (detail.BroadcastLinks.Count > 0) target.BroadcastLinks = detail.BroadcastLinks;

            if (target.HomeScore.HasValue && target.AwayScore.HasValue &&
                (string.IsNullOrWhiteSpace(target.Status) || string.Equals(target.Status, "試合前", StringComparison.Ordinal)))
            {
                target.Status = "試合終了";
            }
        }

        private static bool ShouldUseIncomingDate(string currentValue, string incomingValue)
        {
            if (string.IsNullOrWhiteSpace(incomingValue))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentValue))
            {
                return true;
            }

            var currentAmbiguous = IsAmbiguousDate(currentValue);
            var incomingAmbiguous = IsAmbiguousDate(incomingValue);
            return currentAmbiguous && !incomingAmbiguous;
        }

        private static bool ShouldUseIncomingValue(string currentValue, string incomingValue)
        {
            if (string.IsNullOrWhiteSpace(incomingValue))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(currentValue) ||
                   string.Equals(currentValue, "未定", StringComparison.Ordinal) ||
                   !string.Equals(incomingValue, "未定", StringComparison.Ordinal);
        }

        private static bool IsAmbiguousDate(string value)
        {
            var normalized = Clean(value);
            return normalized.Contains("or", StringComparison.OrdinalIgnoreCase) ||
                   Regex.Matches(normalized, @"\d{1,2}月").Count > 1;
        }

        private static string NormalizeDateKey(string value)
        {
            var normalized = Clean(value)
                .Replace("（", "(", StringComparison.Ordinal)
                .Replace("）", ")", StringComparison.Ordinal);
            return Regex.Replace(normalized, @"\([^)]*\)", "")
                .Replace(" ", "", StringComparison.Ordinal);
        }

        private static List<Item> Deduplicate(IEnumerable<Item> items)
        {
            var result = new List<Item>();
            foreach (var item in items)
            {
                if (result.Any(existing => IsSameFixture(existing, item)))
                {
                    continue;
                }

                result.Add(item);
            }

            return result;
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
                .GroupBy(option => option.SeasonKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static void EnsureSeasonOption(List<SeasonOption> seasons, string seasonKey)
        {
            if (seasons.Any(season => string.Equals(season.SeasonKey, seasonKey, StringComparison.Ordinal)))
            {
                return;
            }

            seasons.Insert(0, new SeasonOption
            {
                SeasonKey = seasonKey,
                SeasonLabel = SeasonCatalog.ToSeasonLabel(seasonKey)
            });
        }

        private static SeasonOption SelectSeason(IReadOnlyList<SeasonOption> seasons, string? requestedKey)
        {
            var selected = !string.IsNullOrWhiteSpace(requestedKey)
                ? seasons.FirstOrDefault(season => string.Equals(season.SeasonKey, requestedKey, StringComparison.Ordinal))
                : seasons.FirstOrDefault(season => string.Equals(season.SeasonKey, SeasonCatalog.CurrentSeasonKey, StringComparison.Ordinal))
                  ?? seasons.FirstOrDefault();

            if (selected != null)
            {
                return selected;
            }

            var key = string.IsNullOrWhiteSpace(requestedKey) ? SeasonCatalog.CurrentSeasonKey : requestedKey.Trim();
            return new SeasonOption
            {
                SeasonKey = key,
                SeasonLabel = SeasonCatalog.ToSeasonLabel(key)
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

        private static List<Item> ParseCategory(HtmlDocument doc, CategoryDefinition category, SeasonOption season, string sourceUrl)
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
                var item = ParseScheduleCard(card, category, season, sourceUrl);
                if (item != null)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private static Item? ParseScheduleCard(HtmlNode card, CategoryDefinition category, SeasonOption season, string sourceUrl)
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
            var seasonStartYear = SeasonCatalog.ParseSeasonStartYear(season.SeasonKey);

            return new Item
            {
                SeasonKey = season.SeasonKey,
                SeasonLabel = season.SeasonLabel,
                SeasonStartYear = seasonStartYear,
                CategoryCode = category.Code,
                CategoryLabel = category.Label,
                Division = category.Label,
                Round = ParseRoundFromTitle(titleText),
                Date = MergeDate(ToJapaneseDate(dateText), BracketDow(dow)),
                Kickoff = kickoff,
                Conference = seasonStartYear >= 2026 && category.Code == "D1" ? "" : ParseConferenceFromTitle(titleText),
                Home = SeasonCatalog.NormalizeTeamName(GetTeamName(homeNode), seasonStartYear),
                Away = SeasonCatalog.NormalizeTeamName(GetTeamName(awayNode), seasonStartYear),
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
                BroadcastLinks = broadcastLinks,
                SourceUrl = sourceUrl
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

        private static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            return NormalizeSpaces(HtmlEntity.DeEntitize(value)
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
