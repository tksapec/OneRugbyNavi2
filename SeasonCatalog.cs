using System;
using System.Collections.Generic;

namespace OneRugbyNavi2;

public static class SeasonCatalog
{
    private const int SeasonBoundaryMonth = 9;

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
}
