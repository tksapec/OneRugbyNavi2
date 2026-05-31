using System.Linq;

namespace OneRugbyNavi2
{
    public class MatchItem
    {
        public string Division { get; set; } = "";
        public string Section { get; set; } = "";
        public string MatchDate { get; set; } = "";
        public string KickoffTime { get; set; } = "";
        public string Conference { get; set; } = "";
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string Prefecture { get; set; } = "";
        public string Venue { get; set; } = "";
        public string VenueDisplayName { get; set; } = "";
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public string MatchStatus { get; set; } = "";
        public string MatchInfoUrl { get; set; } = "";
        public string ReportUrl { get; set; } = "";
        public string? HomeLogoPath { get; set; }
        public string? AwayLogoPath { get; set; }
        public string HomeBadgeText { get; set; } = "";
        public string AwayBadgeText { get; set; } = "";

        public bool HasResult => HomeScore.HasValue && AwayScore.HasValue;
        public bool HasHomeLogo => !string.IsNullOrWhiteSpace(HomeLogoPath);
        public bool HasAwayLogo => !string.IsNullOrWhiteSpace(AwayLogoPath);
        public bool HasHomeBadge => !HasHomeLogo && !string.IsNullOrWhiteSpace(HomeBadgeText);
        public bool HasAwayBadge => !HasAwayLogo && !string.IsNullOrWhiteSpace(AwayBadgeText);
        public string ScoreText
        {
            get
            {
                if (HasResult)
                {
                    return $"{HomeScore} - {AwayScore}";
                }

                foreach (var status in StatusesShownWithoutScore)
                {
                    if (MatchStatus.Contains(status, System.StringComparison.Ordinal))
                    {
                        return MatchStatus;
                    }
                }

                return "vs";
            }
        }

        public string HomeVsAway => $"{HomeTeam} vs {AwayTeam}";
        public string DateTimeCompact => $"{MatchDate}  {KickoffTime}";
        public string VenueCompact => string.IsNullOrWhiteSpace(Prefecture) ? VenueDisplayName : $"{Prefecture}\u30FB{VenueDisplayName}";
        public string StatusText => string.IsNullOrWhiteSpace(MatchStatus) ? (HasResult ? "\u8A66\u5408\u7D42\u4E86" : "\u8A66\u5408\u524D") : MatchStatus;
        public bool IsCompleted => HasResult ||
            StatusText.Contains("\u8A66\u5408\u7D42\u4E86", System.StringComparison.Ordinal) ||
            StatusText.Contains("Full-Time", System.StringComparison.OrdinalIgnoreCase);
        public bool HasStatusBadge => IsCompleted || HasSpecialStatus;
        public bool HasSpecialStatus => !IsCompleted && StatusesShownWithoutScore.Any(status => MatchStatus.Contains(status, System.StringComparison.Ordinal));
        public string StatusBadgeText => IsCompleted ? "\u8A66\u5408\u7D42\u4E86" : MatchStatus;

        private static readonly string[] StatusesShownWithoutScore =
        {
            "\u4E2D\u6B62",
            "\u672A\u5B9A",
            "\u5EF6\u671F",
            "\u9806\u5EF6",
            "\u53D6\u6D88"
        };
    }
}

