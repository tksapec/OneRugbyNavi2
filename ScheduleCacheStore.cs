using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace OneRugbyNavi2
{
    public sealed class ScheduleCacheData
    {
        public int CacheVersion { get; init; }
        public DateTimeOffset LastUpdated { get; init; }
        public List<ScheduleFetcher.Item> Div1 { get; init; } = new();
        public List<ScheduleFetcher.Item> Div2 { get; init; } = new();
        public List<ScheduleFetcher.Item> Div3 { get; init; } = new();
    }

    internal sealed class ScheduleCachePayload
    {
        public int CacheVersion { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public List<ScheduleFetcher.Item> Div1 { get; set; } = new();
        public List<ScheduleFetcher.Item> Div2 { get; set; } = new();
        public List<ScheduleFetcher.Item> Div3 { get; set; } = new();
    }

    public static class ScheduleCacheStore
    {
        private const int CurrentCacheVersion = 2;
        private static readonly string CachePath = Path.Combine(FileSystem.AppDataDirectory, "schedule-cache.json");
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        public static async Task SaveAsync(
            IReadOnlyCollection<ScheduleFetcher.Item> div1,
            IReadOnlyCollection<ScheduleFetcher.Item> div2,
            IReadOnlyCollection<ScheduleFetcher.Item> div3,
            DateTimeOffset lastUpdated)
        {
            var payload = new ScheduleCachePayload
            {
                CacheVersion = CurrentCacheVersion,
                LastUpdated = lastUpdated,
                Div1 = new List<ScheduleFetcher.Item>(div1),
                Div2 = new List<ScheduleFetcher.Item>(div2),
                Div3 = new List<ScheduleFetcher.Item>(div3)
            };

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            await File.WriteAllTextAsync(CachePath, json);
        }

        public static async Task<ScheduleCacheData?> LoadAsync()
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(CachePath);
                var payload = JsonSerializer.Deserialize<ScheduleCachePayload>(json, JsonOptions);
                if (payload == null)
                {
                    return null;
                }

                if (payload.CacheVersion != CurrentCacheVersion)
                {
                    return null;
                }

                if (HasInvalidTeamCache(payload.Div1) ||
                    HasInvalidTeamCache(payload.Div2) ||
                    HasInvalidTeamCache(payload.Div3))
                {
                    return null;
                }

                return new ScheduleCacheData
                {
                    CacheVersion = payload.CacheVersion,
                    LastUpdated = payload.LastUpdated,
                    Div1 = payload.Div1 ?? new List<ScheduleFetcher.Item>(),
                    Div2 = payload.Div2 ?? new List<ScheduleFetcher.Item>(),
                    Div3 = payload.Div3 ?? new List<ScheduleFetcher.Item>()
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
                if (File.Exists(CachePath))
                {
                    File.Delete(CachePath);
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
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

            return System.Text.RegularExpressions.Regex.IsMatch(
                value,
                @"(?<![A-Za-z])(?:v|V|vs|VS|Ｖ|ｖ)(?![A-Za-z])");
        }
    }
}

