using System;
using System.Collections.Generic;
using System.Linq;

namespace OneRugbyNavi2;

public static class SeasonCatalog
{
    private const int SeasonBoundaryMonth = 9;

    public sealed record TeamSeasonInfo(
        string TeamName,
        string DivisionCode,
        string ShortName = "",
        string AreaText = "",
        string LeagueOneTeamId = "",
        string TeamUrl = "",
        string LogoUrl = "");

    public static IReadOnlyList<TeamSeasonInfo> Teams2026 { get; } = new[]
    {
        new TeamSeasonInfo("浦安D-Rocks", "DIV1", LeagueOneTeamId: "126", TeamUrl: "https://league-one.jp/team/126"),
        new TeamSeasonInfo("クボタスピアーズ船橋・東京ベイ", "DIV1", LeagueOneTeamId: "123", TeamUrl: "https://league-one.jp/team/123"),
        new TeamSeasonInfo("コベルコ神戸スティーラーズ", "DIV1", LeagueOneTeamId: "127", TeamUrl: "https://league-one.jp/team/127"),
        new TeamSeasonInfo("埼玉パナソニックワイルドナイツ", "DIV1", LeagueOneTeamId: "128", TeamUrl: "https://league-one.jp/team/128"),
        new TeamSeasonInfo("静岡ブルーレヴズ", "DIV1", LeagueOneTeamId: "124", TeamUrl: "https://league-one.jp/team/124"),
        new TeamSeasonInfo("東京サントリーサンゴリアス", "DIV1", LeagueOneTeamId: "125", TeamUrl: "https://league-one.jp/team/125"),
        new TeamSeasonInfo("東芝ブレイブルーパス武蔵", "DIV1", LeagueOneTeamId: "129", TeamUrl: "https://league-one.jp/team/129"),
        new TeamSeasonInfo("栃木ホンダヒート", "DIV1", LeagueOneTeamId: "131", TeamUrl: "https://league-one.jp/team/131"),
        new TeamSeasonInfo("トヨタヴェルブリッツ", "DIV1", LeagueOneTeamId: "130", TeamUrl: "https://league-one.jp/team/130"),
        new TeamSeasonInfo("三菱重工相模原ダイナボアーズ", "DIV1", LeagueOneTeamId: "132", TeamUrl: "https://league-one.jp/team/132"),
        new TeamSeasonInfo("横浜キヤノンイーグルス", "DIV1", LeagueOneTeamId: "133", TeamUrl: "https://league-one.jp/team/133"),
        new TeamSeasonInfo("リコーブラックラムズ東京", "DIV1", LeagueOneTeamId: "134", TeamUrl: "https://league-one.jp/team/134"),

        new TeamSeasonInfo("清水建設江東ブルーシャークス", "DIV2", LeagueOneTeamId: "137", TeamUrl: "https://league-one.jp/team/137"),
        new TeamSeasonInfo("豊田自動織機シャトルズ愛知", "DIV2", LeagueOneTeamId: "138", TeamUrl: "https://league-one.jp/team/138"),
        new TeamSeasonInfo("花園近鉄ライナーズ", "DIV2", LeagueOneTeamId: "140", TeamUrl: "https://league-one.jp/team/140"),
        new TeamSeasonInfo("レッドハリケーンズ大阪", "DIV2", LeagueOneTeamId: "142", TeamUrl: "https://league-one.jp/team/142"),
        new TeamSeasonInfo("マツダスカイアクティブズ広島", "DIV2", LeagueOneTeamId: "146", TeamUrl: "https://league-one.jp/team/146"),
        new TeamSeasonInfo("JR東日本グリーンウォリアーズ東葛", "DIV2", LeagueOneTeamId: "135", TeamUrl: "https://league-one.jp/team/135"),
        new TeamSeasonInfo("九州電力キューデンヴォルテクス", "DIV2", LeagueOneTeamId: "136", TeamUrl: "https://league-one.jp/team/136"),

        new TeamSeasonInfo("クリタウォーターガッシュ昭島", "DIV3", LeagueOneTeamId: "143", TeamUrl: "https://league-one.jp/team/143"),
        new TeamSeasonInfo("日野レッドドルフィンズ", "DIV3", LeagueOneTeamId: "141", TeamUrl: "https://league-one.jp/team/141"),
        new TeamSeasonInfo("日本製鉄釜石シーウェイブス", "DIV3", LeagueOneTeamId: "139", TeamUrl: "https://league-one.jp/team/139"),
        new TeamSeasonInfo("中国電力レッドレグリオンズ", "DIV3", LeagueOneTeamId: "145", TeamUrl: "https://league-one.jp/team/145"),
        new TeamSeasonInfo("川越狭山セコムラガッツ", "DIV2", LeagueOneTeamId: "144", TeamUrl: "https://league-one.jp/team/144"),
        new TeamSeasonInfo("ルリーロ福岡", "DIV3", LeagueOneTeamId: "148", TeamUrl: "https://league-one.jp/team/148"),
        new TeamSeasonInfo("ヤクルトレビンズ戸田", "DIV3", LeagueOneTeamId: "147", TeamUrl: "https://league-one.jp/team/147"),
        new TeamSeasonInfo("丸和MOMOTARO’S成田", "DIV3", LeagueOneTeamId: "149", TeamUrl: "https://league-one.jp/team/149")
    };

    // League One's team index and schedule/result pages use these source-specific
    // labels for the same clubs in 2026-27. This map is for matching only; parsers
    // retain each page's original team text.
    private static readonly IReadOnlyDictionary<string, string> TeamIdentityAliases2026 =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["埼玉ワイルドナイツ"] = "埼玉パナソニックワイルドナイツ",
            ["東京サンゴリアス"] = "東京サントリーサンゴリアス",
            ["ブラックラムズ東京"] = "リコーブラックラムズ東京",
            ["スカイアクティブズ広島"] = "マツダスカイアクティブズ広島",
            ["グリーンウォリアーズ東葛"] = "JR東日本グリーンウォリアーズ東葛"
        };

    public static int CurrentSeasonStartYear => GetSeasonStartYear(DateTime.Today);

    public static string CurrentSeasonKey => CurrentSeasonStartYear.ToString();

    public static string ToSeasonUiLabel(string? seasonKey)
    {
        var key = seasonKey?.Trim() ?? "";
        return int.TryParse(key, out var year)
            ? $"{year}-{(year + 1) % 100:00} Season"
            : "";
    }

    public static int GetSeasonStartYear(DateTime date)
        => date.Month >= SeasonBoundaryMonth ? date.Year : date.Year - 1;

    public static int ParseSeasonStartYear(string? seasonKey)
        => int.TryParse(seasonKey, out var year) ? year : 0;

    public static string ToSeasonLabel(string? seasonKey)
    {
        var key = seasonKey?.Trim() ?? "";
        if (!int.TryParse(key, out var year))
        {
            return key;
        }

        return year == 2021
            ? "2022シーズン"
            : $"{year}-{(year + 1) % 100:00}シーズン";
    }

    public static string ScheduleTableUrl(string seasonKey, string division)
    {
        var key = seasonKey?.Trim() ?? "";
        var normalizedDivision = division?.Trim().ToLowerInvariant() ?? "";
        var divisionNumber = normalizedDivision switch
        {
            "d1" or "div1" => "1",
            "d2" or "div2" => "2",
            "d3" or "div3" => "3",
            _ => ""
        };
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(divisionNumber))
        {
            return "";
        }

        return $"https://league-one.jp/content/schedule_table/{Uri.EscapeDataString(key)}/div{divisionNumber}";
    }

    public static string NormalizeTeamName(string? teamName, int seasonStartYear)
        => teamName?.Trim() ?? "";

    public static bool AreSameTeamName(string? left, string? right, int seasonStartYear)
    {
        var leftKey = GetTeamIdentityKey(left, seasonStartYear);
        var rightKey = GetTeamIdentityKey(right, seasonStartYear);
        return leftKey.Length > 0 && string.Equals(leftKey, rightKey, StringComparison.Ordinal);
    }

    private static string GetTeamIdentityKey(string? teamName, int seasonStartYear)
    {
        var value = teamName?.Trim() ?? "";
        if (seasonStartYear == 2026 && TeamIdentityAliases2026.TryGetValue(value, out var canonical))
        {
            return canonical;
        }

        if (seasonStartYear == 2026 && value is "三重ホンダヒート" or "NECグリーンロケッツ東葛" or "狭山セコムラガッツ" or "AZ-COM丸和MOMOTARO’S" or "AZ-COM丸和MOMOTARO'S")
        {
            return value switch
            {
                "三重ホンダヒート" => "栃木ホンダヒート",
                "NECグリーンロケッツ東葛" => "JR東日本グリーンウォリアーズ東葛",
                "狭山セコムラガッツ" => "川越狭山セコムラガッツ",
                _ => "丸和MOMOTARO’S成田"
            };
        }

        return value;
    }

    public static TeamSeasonInfo? GetCurrentTeam(string? teamName)
    {
        var value = teamName?.Trim() ?? "";
        return Teams2026.FirstOrDefault(team => string.Equals(team.TeamName, value, StringComparison.Ordinal));
    }

    public static bool HasChangedBranding(string? storedTeamName)
    {
        var value = storedTeamName?.Trim() ?? "";
        return value is "三重ホンダヒート" or "NECグリーンロケッツ東葛" or "グリーンロケッツ東葛" or "狭山セコムラガッツ" or "AZ-COM丸和MOMOTARO’S" or "AZ-COM丸和MOMOTARO'S";
    }
}
