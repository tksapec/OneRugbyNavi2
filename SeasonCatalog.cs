using System;
using System.Collections.Generic;
using System.Linq;

namespace OneRugbyNavi2;

public static class SeasonCatalog
{
    private const int SeasonBoundaryMonth = 9;

    public sealed record TeamSeasonInfo(string TeamName, string DivisionCode, string ShortName = "", string AreaText = "");

    private static readonly IReadOnlyDictionary<string, string> CurrentTeamAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["三重ホンダヒート"] = "栃木ホンダヒート",
            ["NECグリーンロケッツ東葛"] = "JR東日本グリーンウォリアーズ東葛",
            ["グリーンロケッツ東葛"] = "JR東日本グリーンウォリアーズ東葛",
            ["狭山セコムラガッツ"] = "川越狭山セコムラガッツ",
            ["AZ-COM丸和MOMOTARO’S"] = "丸和MOMOTARO’S成田",
            ["AZ-COM丸和MOMOTARO'S"] = "丸和MOMOTARO’S成田"
        };

    public static IReadOnlyList<TeamSeasonInfo> Teams2026 { get; } = new[]
    {
        new TeamSeasonInfo("浦安D-Rocks", "DIV1"),
        new TeamSeasonInfo("クボタスピアーズ船橋・東京ベイ", "DIV1"),
        new TeamSeasonInfo("コベルコ神戸スティーラーズ", "DIV1"),
        new TeamSeasonInfo("埼玉ワイルドナイツ", "DIV1"),
        new TeamSeasonInfo("静岡ブルーレヴズ", "DIV1"),
        new TeamSeasonInfo("東京サンゴリアス", "DIV1"),
        new TeamSeasonInfo("東芝ブレイブルーパス東京", "DIV1"),
        new TeamSeasonInfo("栃木ホンダヒート", "DIV1"),
        new TeamSeasonInfo("トヨタヴェルブリッツ", "DIV1"),
        new TeamSeasonInfo("三菱重工相模原ダイナボアーズ", "DIV1"),
        new TeamSeasonInfo("横浜キヤノンイーグルス", "DIV1"),
        new TeamSeasonInfo("ブラックラムズ東京", "DIV1"),

        new TeamSeasonInfo("清水建設江東ブルーシャークス", "DIV2"),
        new TeamSeasonInfo("豊田自動織機シャトルズ愛知", "DIV2"),
        new TeamSeasonInfo("花園近鉄ライナーズ", "DIV2"),
        new TeamSeasonInfo("レッドハリケーンズ大阪", "DIV2"),
        new TeamSeasonInfo("マツダスカイアクティブズ広島", "DIV2"),
        new TeamSeasonInfo("川越狭山セコムラガッツ", "DIV2"),
        new TeamSeasonInfo("JR東日本グリーンウォリアーズ東葛", "DIV2"),
        new TeamSeasonInfo("九州電力キューデンヴォルテクス", "DIV2"),

        new TeamSeasonInfo("クリタウォーターガッシュ昭島", "DIV3"),
        new TeamSeasonInfo("日野レッドドルフィンズ", "DIV3"),
        new TeamSeasonInfo("日本製鉄釜石シーウェイブス", "DIV3"),
        new TeamSeasonInfo("中国電力レッドレグリオンズ", "DIV3"),
        new TeamSeasonInfo("ルリーロ福岡", "DIV3"),
        new TeamSeasonInfo("ヤクルトレビンズ戸田", "DIV3"),
        new TeamSeasonInfo("丸和MOMOTARO’S成田", "DIV3", "M成田", "千葉県成田市")
    };

    public static int CurrentSeasonStartYear => GetSeasonStartYear(DateTime.Today);

    public static string CurrentSeasonKey => CurrentSeasonStartYear.ToString();

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
        if (string.IsNullOrWhiteSpace(key) ||
            (normalizedDivision != "div1" && normalizedDivision != "div2" && normalizedDivision != "div3"))
        {
            return "";
        }

        return $"https://league-one.jp/content/schedule_table/{Uri.EscapeDataString(key)}/{normalizedDivision}";
    }

    public static string NormalizeTeamName(string? teamName, int seasonStartYear)
    {
        var value = teamName?.Trim() ?? "";
        if (seasonStartYear < 2026 || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return CurrentTeamAliases.TryGetValue(value, out var currentName)
            ? currentName
            : value;
    }

    public static TeamSeasonInfo? GetCurrentTeam(string? teamName)
    {
        var normalized = NormalizeTeamName(teamName, 2026);
        return Teams2026.FirstOrDefault(team => string.Equals(team.TeamName, normalized, StringComparison.Ordinal));
    }

    public static bool HasChangedBranding(string? storedTeamName)
    {
        var value = storedTeamName?.Trim() ?? "";
        return value is "三重ホンダヒート" or "NECグリーンロケッツ東葛" or "グリーンロケッツ東葛" or "狭山セコムラガッツ";
    }
}
