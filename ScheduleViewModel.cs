using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace OneRugbyNavi2
{
    public class ScheduleViewModel
    {
        public enum DateRangeFilter
        {
            All,
            Upcoming,
            Past
        }

        public ObservableCollection<MatchItem> ItemsDiv1 { get; } = new();
        public ObservableCollection<MatchItem> ItemsDiv2 { get; } = new();
        public ObservableCollection<MatchItem> ItemsDiv3 { get; } = new();

        private ObservableCollection<MatchItem>? _currentSource;

        public ObservableCollection<MatchItem> FilteredItems { get; } = new();

        public string? TeamFilter { get; set; }
        public string? VenueFilter { get; set; }
        public DateRangeFilter PeriodFilter { get; set; } = DateRangeFilter.All;
        public int CurrentDivision { get; private set; } = 1;

        public void SetItems(
            IReadOnlyCollection<ScheduleFetcher.Item> div1,
            IReadOnlyCollection<ScheduleFetcher.Item> div2,
            IReadOnlyCollection<ScheduleFetcher.Item> div3)
        {
            ReplaceItems(ItemsDiv1, div1);
            ReplaceItems(ItemsDiv2, div2);
            ReplaceItems(ItemsDiv3, div3);
            SetSource(CurrentDivision);
        }

        public void SetSource(int div)
        {
            CurrentDivision = div;
            _currentSource = div switch
            {
                1 => ItemsDiv1,
                2 => ItemsDiv2,
                _ => ItemsDiv3
            };

            ApplyFilters();
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
                query = query.Where(m =>
                    TryMatchPeriod(m, today, PeriodFilter));
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
            return division switch
            {
                1 => ItemsDiv1.Count,
                2 => ItemsDiv2.Count,
                _ => ItemsDiv3.Count
            };
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

        private static void ReplaceItems(
            ObservableCollection<MatchItem> target,
            IReadOnlyCollection<ScheduleFetcher.Item> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(ToMatch(item));
            }
        }

        private static MatchItem ToMatch(ScheduleFetcher.Item it) => new()
        {
            Division = it.Division,
            Section = it.Round,
            MatchDate = it.Date,
            KickoffTime = it.Kickoff,
            Conference = it.Conference,
            HomeTeam = it.Home,
            AwayTeam = it.Away,
            Prefecture = it.Pref,
            Venue = it.Venue,
            VenueDisplayName = VenueNameResolver.Resolve(it.Pref, it.Venue),
            HomeScore = it.HomeScore,
            AwayScore = it.AwayScore,
            MatchStatus = it.Status,
            MatchInfoUrl = it.MatchInfoUrl,
            ReportUrl = it.ReportUrl,
            HomeLogoPath = TeamLogoResolver.GetLogoPath(it.Home),
            AwayLogoPath = TeamLogoResolver.GetLogoPath(it.Away),
            HomeBadgeText = TeamLogoResolver.GetBadgeText(it.Home),
            AwayBadgeText = TeamLogoResolver.GetBadgeText(it.Away)
        };

        public static bool TryGetMatchDate(MatchItem match, out DateTime date)
        {
            date = default;
            var parsed = Regex.Match(match.MatchDate, @"(?<month>\d{1,2})\u6708(?<day>\d{1,2})\u65E5");
            if (!parsed.Success)
            {
                return false;
            }

            int month = int.Parse(parsed.Groups["month"].Value);
            int day = int.Parse(parsed.Groups["day"].Value);
            int year = month >= 9 ? ScheduleFetcher.SeasonYear : ScheduleFetcher.SeasonYear + 1;
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
    }
}

