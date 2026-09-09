using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace OneRugbyNavi2
{
    public class ScheduleViewModel
    {
        public const string CategoryDiv1 = "D1";
        public const string CategoryDiv2 = "D2";
        public const string CategoryDiv3 = "D3";
        public const string CategoryReplacement = "Replacement";
        public const string CategoryOther = "Other";

        public enum DateRangeFilter
        {
            All,
            Upcoming,
            Past
        }

        public ObservableCollection<MatchItem> ItemsDiv1 { get; } = new();
        public ObservableCollection<MatchItem> ItemsDiv2 { get; } = new();
        public ObservableCollection<MatchItem> ItemsDiv3 { get; } = new();
        public ObservableCollection<MatchItem> ItemsReplacement { get; } = new();
        public ObservableCollection<MatchItem> ItemsOther { get; } = new();

        private readonly Dictionary<string, ObservableCollection<MatchItem>> _itemsByCategory = new(StringComparer.Ordinal)
        {
            [CategoryDiv1] = new ObservableCollection<MatchItem>(),
            [CategoryDiv2] = new ObservableCollection<MatchItem>(),
            [CategoryDiv3] = new ObservableCollection<MatchItem>(),
            [CategoryReplacement] = new ObservableCollection<MatchItem>(),
            [CategoryOther] = new ObservableCollection<MatchItem>()
        };

        private ObservableCollection<MatchItem>? _currentSource;

        public ObservableCollection<MatchItem> FilteredItems { get; } = new();

        public string? TeamFilter { get; set; }
        public string? VenueFilter { get; set; }
        public DateRangeFilter PeriodFilter { get; set; } = DateRangeFilter.All;
        public int CurrentDivision { get; private set; } = 1;
        public string CurrentCategory { get; private set; } = CategoryDiv1;

        public void SetItems(
            IReadOnlyCollection<ScheduleFetcher.Item> div1,
            IReadOnlyCollection<ScheduleFetcher.Item> div2,
            IReadOnlyCollection<ScheduleFetcher.Item> div3)
        {
            SetItems(div1, div2, div3, Array.Empty<ScheduleFetcher.Item>(), Array.Empty<ScheduleFetcher.Item>());
        }

        public void SetItems(
            IReadOnlyCollection<ScheduleFetcher.Item> div1,
            IReadOnlyCollection<ScheduleFetcher.Item> div2,
            IReadOnlyCollection<ScheduleFetcher.Item> div3,
            IReadOnlyCollection<ScheduleFetcher.Item> replacement,
            IReadOnlyCollection<ScheduleFetcher.Item> other)
        {
            ReplaceItems(CategoryDiv1, div1);
            ReplaceItems(CategoryDiv2, div2);
            ReplaceItems(CategoryDiv3, div3);
            ReplaceItems(CategoryReplacement, replacement);
            ReplaceItems(CategoryOther, other);

            SyncLegacyCollections();
            SetSource(CurrentCategory);
        }

        public void SetSource(int div)
        {
            SetSource(DivisionToCategory(div));
        }

        public void SetSource(string category)
        {
            CurrentCategory = NormalizeCategoryCode(category);
            CurrentDivision = CategoryToDivision(CurrentCategory);
            _currentSource = _itemsByCategory.TryGetValue(CurrentCategory, out var source)
                ? source
                : _itemsByCategory[CategoryOther];

            ApplyFilters();
        }

        public IReadOnlyList<string> GetVisibleCategories()
        {
            var categories = new List<string> { CategoryDiv1, CategoryDiv2, CategoryDiv3, CategoryReplacement };
            if (GetCategoryItemCount(CategoryOther) > 0)
            {
                categories.Add(CategoryOther);
            }

            return categories;
        }

        public void ApplyFilters()
        {
            if (_currentSource is null)
            {
                return;
            }

            var query = _currentSource.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(TeamFilter))
            {
                query = query.Where(m => m.HomeTeam == TeamFilter || m.AwayTeam == TeamFilter);
            }

            if (!string.IsNullOrWhiteSpace(VenueFilter))
            {
                query = query.Where(m =>
                    m.VenueCompact == VenueFilter ||
                    m.Prefecture == VenueFilter ||
                    m.Venue == VenueFilter);
            }

            if (PeriodFilter != DateRangeFilter.All)
            {
                var today = DateTime.Today;
                query = query.Where(m => TryMatchPeriod(m, today, PeriodFilter));
            }

            var list = SortMatches(query, DateTime.Today, PeriodFilter).ToList();
            FilteredItems.Clear();
            foreach (var item in list)
            {
                FilteredItems.Add(item);
            }
        }

        public int GetCurrentDivisionItemCount()
        {
            return _currentSource?.Count ?? 0;
        }

        public int GetDivisionItemCount(int division)
        {
            return GetCategoryItemCount(DivisionToCategory(division));
        }

        public int GetCategoryItemCount(string category)
        {
            return _itemsByCategory.TryGetValue(NormalizeCategoryCode(category), out var source) ? source.Count : 0;
        }

        public MatchItem? GetNextMatch(string? preferredTeam = null)
        {
            if (_currentSource is null)
            {
                return null;
            }

            var today = DateTime.Today;
            var team = !string.IsNullOrWhiteSpace(TeamFilter) ? TeamFilter : preferredTeam;
            if (!string.IsNullOrWhiteSpace(team))
            {
                var teamNextMatch = FindNextMatch(
                    _currentSource.Where(m => m.HomeTeam == team || m.AwayTeam == team),
                    today);
                if (teamNextMatch != null)
                {
                    return teamNextMatch;
                }
            }

            return FindNextMatch(_currentSource, today);
        }

        private static MatchItem? FindNextMatch(IEnumerable<MatchItem> matches, DateTime today)
        {
            return matches
                .Select(match => new { Match = match, HasDate = TryGetMatchDate(match, out var date), Date = date })
                .Where(item => item.HasDate && (item.Date > today || (item.Date == today && !item.Match.IsCompleted)))
                .OrderBy(item => item.Date)
                .ThenBy(item => TryGetMatchStart(item.Match, out var start) ? start.TimeOfDay : TimeSpan.MaxValue)
                .Select(item => item.Match)
                .FirstOrDefault();
        }

        public List<string> GetTeamsForPicker()
        {
            if (_currentSource is null)
            {
                return new List<string> { "" };
            }

            var teams = _currentSource
                .SelectMany(m => new[] { m.HomeTeam, m.AwayTeam })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            teams.Insert(0, "");
            return teams;
        }

        public List<string> GetVenuesForPicker()
        {
            if (_currentSource is null)
            {
                return new List<string> { "" };
            }

            var venues = _currentSource
                .Select(m => m.VenueCompact)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            venues.Insert(0, "");
            return venues;
        }

        public static string GetCategoryLabel(string category)
        {
            return NormalizeCategoryCode(category) switch
            {
                CategoryDiv1 => "D1",
                CategoryDiv2 => "D2",
                CategoryDiv3 => "D3",
                CategoryReplacement => "入替戦",
                _ => "その他"
            };
        }

        private void ReplaceItems(
            string category,
            IReadOnlyCollection<ScheduleFetcher.Item> source)
        {
            var target = _itemsByCategory[NormalizeCategoryCode(category)];
            target.Clear();
            foreach (var item in source)
            {
                target.Add(ToMatch(item));
            }
        }

        private void SyncLegacyCollections()
        {
            CopyCollection(ItemsDiv1, _itemsByCategory[CategoryDiv1]);
            CopyCollection(ItemsDiv2, _itemsByCategory[CategoryDiv2]);
            CopyCollection(ItemsDiv3, _itemsByCategory[CategoryDiv3]);
            CopyCollection(ItemsReplacement, _itemsByCategory[CategoryReplacement]);
            CopyCollection(ItemsOther, _itemsByCategory[CategoryOther]);
        }

        private static void CopyCollection(ObservableCollection<MatchItem> target, IEnumerable<MatchItem> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private static MatchItem ToMatch(ScheduleFetcher.Item item)
        {
            var categorySource = !string.IsNullOrWhiteSpace(item.CategoryCode)
                ? item.CategoryCode
                : item.Division;
            var category = NormalizeCategoryCode(categorySource);
            var seasonStartYear = item.SeasonStartYear > 0
                ? item.SeasonStartYear
                : InferSeasonStartYear(item.Date);
            var seasonKey = !string.IsNullOrWhiteSpace(item.SeasonKey)
                ? item.SeasonKey
                : seasonStartYear > 0 ? seasonStartYear.ToString() : "";
            var seasonLabel = !string.IsNullOrWhiteSpace(item.SeasonLabel)
                ? item.SeasonLabel
                : SeasonCatalog.ToSeasonLabel(seasonKey);
            var home = SeasonCatalog.NormalizeTeamName(item.Home, seasonStartYear);
            var away = SeasonCatalog.NormalizeTeamName(item.Away, seasonStartYear);

            return new MatchItem
            {
                SeasonKey = seasonKey,
                SeasonLabel = seasonLabel,
                SeasonStartYear = seasonStartYear,
                CategoryCode = category,
                CategoryLabel = string.IsNullOrWhiteSpace(item.CategoryLabel) ? GetCategoryLabel(category) : item.CategoryLabel,
                Division = string.IsNullOrWhiteSpace(item.Division) ? GetCategoryLabel(category) : item.Division,
                Section = item.Round,
                MatchDate = item.Date,
                KickoffTime = item.Kickoff,
                Conference = seasonStartYear >= 2026 && category == CategoryDiv1 ? "" : item.Conference,
                HomeTeam = home,
                AwayTeam = away,
                Prefecture = item.Pref,
                Venue = item.Venue,
                VenueDisplayName = VenueNameResolver.Resolve(item.Pref, item.Venue),
                HomeScore = item.HomeScore,
                AwayScore = item.AwayScore,
                MatchStatus = item.Status,
                MatchId = item.MatchId,
                MatchCode = item.MatchCode,
                MatchInfoUrl = item.MatchInfoUrl,
                PreviewUrl = item.PreviewUrl,
                ReportUrl = item.ReportUrl,
                BroadcastText = item.BroadcastText,
                SourceUrl = item.SourceUrl,
                HomeLogoPath = TeamLogoResolver.GetLogoPath(home),
                AwayLogoPath = TeamLogoResolver.GetLogoPath(away),
                HomeBadgeText = TeamLogoResolver.GetBadgeText(home),
                AwayBadgeText = TeamLogoResolver.GetBadgeText(away)
            };
        }

        public static bool TryGetMatchDate(MatchItem match, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(match.MatchDate) || IsAmbiguousDateText(match.MatchDate))
            {
                return false;
            }

            var parsed = Regex.Match(match.MatchDate, @"(?<month>\d{1,2})月(?<day>\d{1,2})日");
            if (!parsed.Success)
            {
                return false;
            }

            int month = int.Parse(parsed.Groups["month"].Value);
            int day = int.Parse(parsed.Groups["day"].Value);
            if (match.SeasonStartYear <= 0)
            {
                return false;
            }

            var seasonStartYear = match.SeasonStartYear;
            int year = month >= 9 ? seasonStartYear : seasonStartYear + 1;
            try
            {
                date = new DateTime(year, month, day);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetMatchStart(MatchItem match, out DateTime start)
        {
            start = default;
            if (!TryGetMatchDate(match, out var date))
            {
                return false;
            }

            var parsed = Regex.Match(match.KickoffTime, @"(?<hour>\d{1,2}):(?<minute>\d{2})");
            if (!parsed.Success)
            {
                return false;
            }

            start = date
                .AddHours(int.Parse(parsed.Groups["hour"].Value))
                .AddMinutes(int.Parse(parsed.Groups["minute"].Value));
            return true;
        }

        private static bool TryMatchPeriod(MatchItem match, DateTime today, DateRangeFilter filter)
        {
            if (!TryGetMatchDate(match, out var matchDate))
            {
                return false;
            }

            var finishedTodayOrEarlier = matchDate < today || (matchDate == today && IsCompletedMatch(match));
            if (filter == DateRangeFilter.Past)
            {
                return finishedTodayOrEarlier;
            }

            return matchDate > today || (matchDate == today && !IsCompletedMatch(match));
        }

        private static bool IsCompletedMatch(MatchItem match)
        {
            return match.IsCompleted;
        }

        private static IEnumerable<MatchItem> SortMatches(IEnumerable<MatchItem> matches, DateTime today, DateRangeFilter filter)
        {
            var sortable = matches
                .Select(match => new
                {
                    Match = match,
                    HasDate = TryGetMatchDate(match, out var date),
                    Date = date
                });

            if (filter == DateRangeFilter.Upcoming)
            {
                return sortable
                    .OrderBy(item => item.HasDate ? 0 : 1)
                    .ThenBy(item => item.Date)
                    .Select(item => item.Match);
            }

            if (filter == DateRangeFilter.Past)
            {
                return sortable
                    .OrderBy(item => item.HasDate ? 0 : 1)
                    .ThenByDescending(item => item.Date)
                    .Select(item => item.Match);
            }

            return sortable
                .OrderBy(item => item.HasDate ? 0 : 1)
                .ThenBy(item => item.HasDate && (item.Date > today || (item.Date == today && !item.Match.IsCompleted)) ? 0 : 1)
                .ThenBy(item => item.HasDate && (item.Date > today || (item.Date == today && !item.Match.IsCompleted)) ? item.Date.Ticks : -item.Date.Ticks)
                .Select(item => item.Match);
        }

        private static bool IsAmbiguousDateText(string value)
        {
            return value.Contains("or", StringComparison.OrdinalIgnoreCase) ||
                   Regex.Matches(value, @"\d{1,2}月").Count > 1;
        }

        private static int InferSeasonStartYear(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || IsAmbiguousDateText(value))
            {
                return 0;
            }

            if (DateTime.TryParse(value, out var fullDate))
            {
                return SeasonCatalog.GetSeasonStartYear(fullDate);
            }

            var yearMatch = Regex.Match(value, @"(?<year>20\d{2})[-/.年](?<month>\d{1,2})");
            if (yearMatch.Success &&
                int.TryParse(yearMatch.Groups["year"].Value, out var year) &&
                int.TryParse(yearMatch.Groups["month"].Value, out var month))
            {
                return month >= 9 ? year : year - 1;
            }

            return 0;
        }

        private static string DivisionToCategory(int division)
        {
            return division switch
            {
                1 => CategoryDiv1,
                2 => CategoryDiv2,
                3 => CategoryDiv3,
                4 => CategoryReplacement,
                _ => CategoryOther
            };
        }

        private static int CategoryToDivision(string category)
        {
            return NormalizeCategoryCode(category) switch
            {
                CategoryDiv1 => 1,
                CategoryDiv2 => 2,
                CategoryDiv3 => 3,
                CategoryReplacement => 4,
                _ => 5
            };
        }

        public static string NormalizeCategoryCode(string category)
        {
            var value = category ?? "";
            if (string.Equals(value, CategoryDiv1, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "DIV1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "DIVISION 1", StringComparison.OrdinalIgnoreCase))
            {
                return CategoryDiv1;
            }

            if (string.Equals(value, CategoryDiv2, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "DIV2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "DIVISION 2", StringComparison.OrdinalIgnoreCase))
            {
                return CategoryDiv2;
            }

            if (string.Equals(value, CategoryDiv3, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "DIV3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "DIVISION 3", StringComparison.OrdinalIgnoreCase))
            {
                return CategoryDiv3;
            }

            if (string.Equals(value, CategoryReplacement, StringComparison.OrdinalIgnoreCase) ||
                value.Contains("入替", StringComparison.Ordinal))
            {
                return CategoryReplacement;
            }

            return CategoryOther;
        }
    }
}
