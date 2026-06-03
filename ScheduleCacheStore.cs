using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace OneRugbyNavi2
{
    public sealed class ScheduleCacheData
    {
        public int CacheVersion { get; init; }
        public DateTimeOffset SavedAt { get; init; }
        public string SeasonLabel { get; init; } = "";
        public string SeasonKey { get; init; } = "";
        public List<ScheduleFetcher.Item> Div1 { get; init; } = new();
        public List<ScheduleFetcher.Item> Div2 { get; init; } = new();
        public List<ScheduleFetcher.Item> Div3 { get; init; } = new();
        public List<ScheduleFetcher.Item> Replacement { get; init; } = new();
        public List<ScheduleFetcher.Item> Other { get; init; } = new();

        public DateTimeOffset LastUpdated => SavedAt;
    }

    internal sealed class ScheduleCachePayload
    {
        public int CacheVersion { get; set; }
        public DateTimeOffset SavedAt { get; set; }
        public string SeasonLabel { get; set; } = "";
        public string SeasonKey { get; set; } = "";
        public List<ScheduleFetcher.Item> Div1 { get; set; } = new();
        public List<ScheduleFetcher.Item> Div2 { get; set; } = new();
        public List<ScheduleFetcher.Item> Div3 { get; set; } = new();
        public List<ScheduleFetcher.Item> Replacement { get; set; } = new();
        public List<ScheduleFetcher.Item> Other { get; set; } = new();
    }

    public static class ScheduleCacheStore
    {
        private const int CurrentCacheVersion = 3;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        public static Task SaveAsync(
            IReadOnlyCollection<ScheduleFetcher.Item> div1,
            IReadOnlyCollection<ScheduleFetcher.Item> div2,
            IReadOnlyCollection<ScheduleFetcher.Item> div3,
            DateTimeOffset lastUpdated)
        {
            var seasonKey = div1.Concat(div2).Concat(div3).FirstOrDefault()?.SeasonKey ?? ScheduleFetcher.SeasonYear.ToString();
            var seasonLabel = div1.Concat(div2).Concat(div3).FirstOrDefault()?.SeasonLabel ?? "";
            return SaveAsync(seasonKey, seasonLabel, div1, div2, div3, Array.Empty<ScheduleFetcher.Item>(), Array.Empty<ScheduleFetcher.Item>(), lastUpdated);
        }

        public static async Task SaveAsync(
            string seasonKey,
            string seasonLabel,
            IReadOnlyCollection<ScheduleFetcher.Item> div1,
            IReadOnlyCollection<ScheduleFetcher.Item> div2,
            IReadOnlyCollection<ScheduleFetcher.Item> div3,
            IReadOnlyCollection<ScheduleFetcher.Item> replacement,
            IReadOnlyCollection<ScheduleFetcher.Item> other,
            DateTimeOffset savedAt)
        {
            var payload = new ScheduleCachePayload
            {
                CacheVersion = CurrentCacheVersion,
                SavedAt = savedAt,
                SeasonKey = seasonKey,
                SeasonLabel = seasonLabel,
                Div1 = new List<ScheduleFetcher.Item>(div1),
                Div2 = new List<ScheduleFetcher.Item>(div2),
                Div3 = new List<ScheduleFetcher.Item>(div3),
                Replacement = new List<ScheduleFetcher.Item>(replacement),
                Other = new List<ScheduleFetcher.Item>(other)
            };

            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            await File.WriteAllTextAsync(GetCachePath(seasonKey, seasonLabel), json);
        }

        public static Task<ScheduleCacheData?> LoadAsync() => LoadAsync(ScheduleFetcher.SeasonYear.ToString(), "");

        public static async Task<ScheduleCacheData?> LoadAsync(string seasonKey, string seasonLabel)
        {
            var path = GetCachePath(seasonKey, seasonLabel);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var payload = JsonSerializer.Deserialize<ScheduleCachePayload>(json, JsonOptions);
                if (payload == null || payload.CacheVersion != CurrentCacheVersion)
                {
                    return null;
                }

                if (HasInvalidTeamCache(payload.Div1) ||
                    HasInvalidTeamCache(payload.Div2) ||
                    HasInvalidTeamCache(payload.Div3) ||
                    HasInvalidTeamCache(payload.Replacement) ||
                    HasInvalidTeamCache(payload.Other))
                {
                    return null;
                }

                return new ScheduleCacheData
                {
                    CacheVersion = payload.CacheVersion,
                    SavedAt = payload.SavedAt,
                    SeasonKey = payload.SeasonKey,
                    SeasonLabel = payload.SeasonLabel,
                    Div1 = payload.Div1 ?? new List<ScheduleFetcher.Item>(),
                    Div2 = payload.Div2 ?? new List<ScheduleFetcher.Item>(),
                    Div3 = payload.Div3 ?? new List<ScheduleFetcher.Item>(),
                    Replacement = payload.Replacement ?? new List<ScheduleFetcher.Item>(),
                    Other = payload.Other ?? new List<ScheduleFetcher.Item>()
                };
            }
            catch
            {
                return null;
            }
        }

        public static Task<bool> DeleteAsync()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(FileSystem.AppDataDirectory, "schedule_cache_*.json"))
                {
                    File.Delete(file);
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public static string GetCachePath(string seasonKey, string seasonLabel)
        {
            var token = !string.IsNullOrWhiteSpace(seasonLabel)
                ? seasonLabel.Replace("シーズン", "", StringComparison.Ordinal)
                : seasonKey;
            return Path.Combine(FileSystem.AppDataDirectory, $"schedule_cache_{SafeFileToken(token)}.json");
        }

        private static string SafeFileToken(string value)
        {
            var normalized = value
                .Replace("ｰ", "-", StringComparison.Ordinal)
                .Replace("－", "-", StringComparison.Ordinal)
                .Replace("ー", "-", StringComparison.Ordinal);
            normalized = Regex.Replace(normalized, @"[^0-9A-Za-z_\-]", "");
            return string.IsNullOrWhiteSpace(normalized) ? "latest" : normalized;
        }

        private static bool HasInvalidTeamCache(IEnumerable<ScheduleFetcher.Item>? items)
        {
            if (items == null)
            {
                return false;
            }

            foreach (var item in items)
            {
                if (LooksLikeMatchCard(item.Home) || LooksLikeMatchCard(item.Away))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeMatchCard(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Regex.IsMatch(
                value,
                @"(?<![A-Za-z])(?:v|V|vs|VS|Ｖ|ｖ)(?![A-Za-z])");
        }
    }
}
