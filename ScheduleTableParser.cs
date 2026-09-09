using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace OneRugbyNavi2;

internal static class ScheduleTableParser
{
    private static readonly Regex RoundRegex = new(@"^第\s*\d+\s*節$", RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"\d{1,2}月.*日", RegexOptions.Compiled);
    private static readonly Regex KickoffRegex = new(@"^(?:\d{1,2}:\d{2}|未定)$", RegexOptions.Compiled);
    private static readonly Regex WeekdayRegex = new(@"^(?:月|火|水|木|金|土|日)(?:or(?:月|火|水|木|金|土|日))*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ScheduleFetcher.Item> Parse(
        string html,
        string seasonKey,
        string divisionCode,
        string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new List<ScheduleFetcher.Item>();
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var seasonStartYear = SeasonCatalog.ParseSeasonStartYear(seasonKey);
        var seasonLabel = SeasonCatalog.ToSeasonLabel(seasonKey);
        var categoryCode = NormalizeDivisionCode(divisionCode);
        var rows = document.DocumentNode.SelectNodes("//table//tr");
        if (rows == null)
        {
            return new List<ScheduleFetcher.Item>();
        }

        var result = new List<ScheduleFetcher.Item>();
        var currentRound = "";

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./th|./td")?
                .Select(cell => Clean(cell.InnerText))
                .ToList();
            if (cells == null || cells.Count == 0 || IsHeaderRow(cells))
            {
                continue;
            }

            var round = cells.FirstOrDefault(IsRoundText);
            if (!string.IsNullOrWhiteSpace(round))
            {
                currentRound = NormalizeRound(round);
            }

            var teamCellIndex = -1;
            string home = "";
            string away = "";
            for (var index = 0; index < cells.Count; index++)
            {
                if (TrySplitTeams(cells[index], out home, out away))
                {
                    teamCellIndex = index;
                    break;
                }
            }

            if (teamCellIndex < 0)
            {
                continue;
            }

            var date = FindDate(cells, teamCellIndex);
            var weekday = FindWeekday(cells, teamCellIndex, date);
            var kickoff = FindKickoff(cells, teamCellIndex);
            var (prefecture, venue) = FindPlace(cells, teamCellIndex);

            home = SeasonCatalog.NormalizeTeamName(home, seasonStartYear);
            away = SeasonCatalog.NormalizeTeamName(away, seasonStartYear);
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            {
                continue;
            }

            result.Add(new ScheduleFetcher.Item
            {
                SeasonKey = seasonKey,
                SeasonLabel = seasonLabel,
                SeasonStartYear = seasonStartYear,
                CategoryCode = categoryCode,
                CategoryLabel = categoryCode,
                Division = categoryCode,
                Round = currentRound,
                Date = MergeDate(date, weekday),
                Kickoff = kickoff,
                Conference = "",
                Home = home,
                Away = away,
                Pref = prefecture,
                Venue = venue,
                Status = string.Equals(kickoff, "未定", StringComparison.Ordinal) ? "未定" : "試合前",
                SourceUrl = sourceUrl
            });
        }

        return result;
    }

    private static bool IsHeaderRow(IReadOnlyCollection<string> cells)
    {
        var joined = string.Join(" ", cells);
        return joined.Contains("ホストチーム", StringComparison.Ordinal) ||
               joined.Contains("ビジターチーム", StringComparison.Ordinal) ||
               joined.Contains("キックオフ", StringComparison.Ordinal) ||
               joined.Contains("開催会場", StringComparison.Ordinal);
    }

    private static bool IsRoundText(string value)
        => RoundRegex.IsMatch(value.Replace(" ", "", StringComparison.Ordinal));

    private static string NormalizeRound(string value)
        => value.Replace(" ", "", StringComparison.Ordinal);

    private static string FindDate(IReadOnlyList<string> cells, int teamCellIndex)
    {
        for (var index = Math.Min(teamCellIndex - 1, cells.Count - 1); index >= 0; index--)
        {
            if (DateRegex.IsMatch(cells[index]))
            {
                return cells[index];
            }
        }

        return "";
    }

    private static string FindWeekday(IReadOnlyList<string> cells, int teamCellIndex, string date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return "";
        }

        var dateIndex = -1;
        for (var index = 0; index < teamCellIndex; index++)
        {
            if (string.Equals(cells[index], date, StringComparison.Ordinal))
            {
                dateIndex = index;
                break;
            }
        }

        if (dateIndex < 0)
        {
            return "";
        }

        for (var index = dateIndex + 1; index < teamCellIndex; index++)
        {
            var value = cells[index].Replace(" ", "", StringComparison.Ordinal);
            if (WeekdayRegex.IsMatch(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string FindKickoff(IReadOnlyList<string> cells, int teamCellIndex)
    {
        for (var index = teamCellIndex - 1; index >= 0; index--)
        {
            if (KickoffRegex.IsMatch(cells[index]))
            {
                return cells[index];
            }
        }

        return "";
    }

    private static (string Prefecture, string Venue) FindPlace(IReadOnlyList<string> cells, int teamCellIndex)
    {
        var placeCells = cells
            .Skip(teamCellIndex + 1)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (placeCells.Count == 0)
        {
            return ("", "");
        }

        if (placeCells.Count == 1)
        {
            return ("", placeCells[0]);
        }

        return (placeCells[0], placeCells[1]);
    }

    private static bool TrySplitTeams(string value, out string home, out string away)
    {
        home = "";
        away = "";

        var text = Clean(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var pattern in new[]
        {
            @"^(?<home>.+?)\s*(?:VS|vs|ＶＳ|ｖｓ)\s*(?<away>.+)$",
            @"^(?<home>.+?)\s+[vVｖＶ]\s+(?<away>.+)$",
            @"^(?<home>.+?)[vｖ](?<away>.+)$"
        })
        {
            var match = Regex.Match(text, pattern);
            if (!match.Success)
            {
                continue;
            }

            home = Clean(match.Groups["home"].Value);
            away = Clean(match.Groups["away"].Value);
            if (!string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(away))
            {
                return true;
            }
        }

        return false;
    }

    private static string MergeDate(string date, string weekday)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(weekday))
        {
            return date;
        }

        return $"{date}({weekday})";
    }

    private static string NormalizeDivisionCode(string divisionCode)
    {
        var value = divisionCode?.Trim() ?? "";
        if (value.Equals("DIV1", StringComparison.OrdinalIgnoreCase) || value.Equals("D1", StringComparison.OrdinalIgnoreCase))
        {
            return "D1";
        }

        if (value.Equals("DIV2", StringComparison.OrdinalIgnoreCase) || value.Equals("D2", StringComparison.OrdinalIgnoreCase))
        {
            return "D2";
        }

        if (value.Equals("DIV3", StringComparison.OrdinalIgnoreCase) || value.Equals("D3", StringComparison.OrdinalIgnoreCase))
        {
            return "D3";
        }

        return value;
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var decoded = HtmlEntity.DeEntitize(value)
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
