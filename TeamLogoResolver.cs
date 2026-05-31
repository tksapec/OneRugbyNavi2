using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OneRugbyNavi2
{
    public static class TeamLogoResolver
    {
        private static readonly Dictionary<string, string> Logos = new()
        {
            ["\u6D66\u5B89D-Rocks"] = "urayasu_d_rocks.jpg",
            ["\u30AF\u30DC\u30BF\u30B9\u30D4\u30A2\u30FC\u30BA\u8239\u6A4B\u30FB\u6771\u4EAC\u30D9\u30A4"] = "kubota_spears_funabashi_tokyo_bay.png",
            ["\u30B3\u30D9\u30EB\u30B3\u795E\u6238\u30B9\u30C6\u30A3\u30FC\u30E9\u30FC\u30BA"] = "kobelco_kobe_steelers.png",
            ["\u57FC\u7389\u30EF\u30A4\u30EB\u30C9\u30CA\u30A4\u30C4"] = "saitama_wild_knights.png",
            ["\u57FC\u7389\u30D1\u30CA\u30BD\u30CB\u30C3\u30AF\u30EF\u30A4\u30EB\u30C9\u30CA\u30A4\u30C4"] = "saitama_wild_knights.png",
            ["\u9759\u5CA1\u30D6\u30EB\u30FC\u30EC\u30F4\u30BA"] = "shizuoka_blue_revs.png",
            ["\u6771\u4EAC\u30B5\u30F3\u30B4\u30EA\u30A2\u30B9"] = "tokyo_sungoliath.png",
            ["\u6771\u4EAC\u30B5\u30F3\u30C8\u30EA\u30FC\u30B5\u30F3\u30B4\u30EA\u30A2\u30B9"] = "tokyo_sungoliath.png",
            ["\u6771\u829D\u30D6\u30EC\u30A4\u30D6\u30EB\u30FC\u30D1\u30B9\u6771\u4EAC"] = "toshiba_brave_lupus_tokyo.png",
            ["\u30C8\u30E8\u30BF\u30F4\u30A7\u30EB\u30D6\u30EA\u30C3\u30C4"] = "toyota_verblitz.png",
            ["\u4E09\u91CD\u30DB\u30F3\u30C0\u30D2\u30FC\u30C8"] = "mie_honda_heat.png",
            ["\u4E09\u83F1\u91CD\u5DE5\u76F8\u6A21\u539F\u30C0\u30A4\u30CA\u30DC\u30A2\u30FC\u30BA"] = "mitsubishi_sagamihara_dynaboars.png",
            ["\u6A2A\u6D5C\u30AD\u30E4\u30CE\u30F3\u30A4\u30FC\u30B0\u30EB\u30B9"] = "yokohama_canon_eagles.png",
            ["\u30EA\u30B3\u30FC\u30D6\u30E9\u30C3\u30AF\u30E9\u30E0\u30BA\u6771\u4EAC"] = "ricoh_blackrams_tokyo.png",
            ["\u30D6\u30E9\u30C3\u30AF\u30E9\u30E0\u30BA\u6771\u4EAC"] = "ricoh_blackrams_tokyo.png",
            ["\u30B0\u30EA\u30FC\u30F3\u30ED\u30B1\u30C3\u30C4\u6771\u845B"] = "green_rockets_tokatsu.png",
            ["NEC\u30B0\u30EA\u30FC\u30F3\u30ED\u30B1\u30C3\u30C4\u6771\u845B"] = "green_rockets_tokatsu.png",
            ["\u4E5D\u5DDE\u96FB\u529B\u30AD\u30E5\u30FC\u30C7\u30F3\u30F4\u30A9\u30EB\u30C6\u30AF\u30B9"] = "kyuden_voltex.png",
            ["\u6E05\u6C34\u5EFA\u8A2D\u6C5F\u6771\u30D6\u30EB\u30FC\u30B7\u30E3\u30FC\u30AF\u30B9"] = "shimizu_koto_blue_sharks.png",
            ["\u8C4A\u7530\u81EA\u52D5\u7E54\u6A5F\u30B7\u30E3\u30C8\u30EB\u30BA\u611B\u77E5"] = "toyota_shuttles_aichi.png",
            ["\u65E5\u672C\u88FD\u9244\u91DC\u77F3\u30B7\u30FC\u30A6\u30A7\u30A4\u30D6\u30B9"] = "nippon_steel_kamaishi_seawaves.png",
            ["\u82B1\u5712\u8FD1\u9244\u30E9\u30A4\u30CA\u30FC\u30BA"] = "hanazono_kintetsu_liners.png",
            ["\u65E5\u91CE\u30EC\u30C3\u30C9\u30C9\u30EB\u30D5\u30A3\u30F3\u30BA"] = "hino_red_dolphins.png",
            ["\u30EC\u30C3\u30C9\u30CF\u30EA\u30B1\u30FC\u30F3\u30BA\u5927\u962A"] = "red_hurricanes_osaka.png",
            ["\u30AF\u30EA\u30BF\u30A6\u30A9\u30FC\u30BF\u30FC\u30AC\u30C3\u30B7\u30E5\u662D\u5CF6"] = "kurita_water_gush_akishima.png",
            ["\u72ED\u5C71\u30BB\u30B3\u30E0\u30E9\u30AC\u30C3\u30C4"] = "sayama_secom_rugguts.png",
            ["\u4E2D\u56FD\u96FB\u529B\u30EC\u30C3\u30C9\u30EC\u30B0\u30EA\u30AA\u30F3\u30BA"] = "chugoku_electric_power_red_reglions.png",
            ["\u30B9\u30AB\u30A4\u30A2\u30AF\u30C6\u30A3\u30D6\u30BA\u5E83\u5CF6"] = "skyactivs_hiroshima.png",
            ["\u30DE\u30C4\u30C0\u30B9\u30AB\u30A4\u30A2\u30AF\u30C6\u30A3\u30D6\u30BA\u5E83\u5CF6"] = "skyactivs_hiroshima.png",
            ["\u30E4\u30AF\u30EB\u30C8\u30EC\u30D3\u30F3\u30BA\u6238\u7530"] = "yakult_levins_toda.png",
            ["\u30EB\u30EA\u30FC\u30ED\u798F\u5CA1"] = "lelir_fukuoka.png"
        };

        public static string? GetLogoPath(string? teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
            {
                return null;
            }

            return Logos.TryGetValue(teamName.Trim(), out var fileName)
                ? fileName
                : null;
        }

        public static string GetBadgeText(string? teamName)
        {
            var normalized = teamName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "?";
            }

            var latinPrefix = Regex.Match(normalized, @"^[A-Za-z0-9\-]+");
            if (latinPrefix.Success)
            {
                return latinPrefix.Value.ToUpperInvariant();
            }

            var elements = StringInfo.GetTextElementEnumerator(normalized);
            var text = "";
            while (elements.MoveNext() && text.Length < 2)
            {
                text += elements.GetTextElement();
            }

            return string.IsNullOrWhiteSpace(text)
                ? normalized.Take(2).Aggregate("", (current, c) => current + c)
                : text;
        }
    }
}

