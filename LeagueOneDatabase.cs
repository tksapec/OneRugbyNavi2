using Microsoft.Data.Sqlite;
using System.Globalization;

namespace OneRugbyNavi2;

public sealed class LeagueOneDatabase
{
    public const string DatabaseFileName = "leagueone.db";
    public const string SeedDatabaseFileName = "leagueone_seed.db";
    private const string CacheManifestFileName = "cache_manifest.txt";
    private const string CacheVersionFileName = ".cache_seed_version";

    public static string AppDataRoot { get; } = Path.Combine(FileSystem.AppDataDirectory, "OneRugbyNavi2");

    private static string DatabasePath => Path.Combine(AppDataRoot, DatabaseFileName);
    private static string CacheRoot => Path.Combine(AppDataRoot, "cache");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(AppDataRoot);
        var seedTempPath = Path.Combine(AppDataRoot, "leagueone_seed_compare.tmp.db");
        await CopyPackageFileAsync(SeedDatabaseFileName, seedTempPath);

        var shouldReplace = !File.Exists(DatabasePath) || IsSeedNewer(seedTempPath, DatabasePath);
        if (shouldReplace)
        {
            ReplaceDatabase(seedTempPath, DatabasePath);
        }
        else if (File.Exists(seedTempPath))
        {
            File.Delete(seedTempPath);
        }

        await EnsureCacheAsync(force: shouldReplace);
    }

    public string GetDatabasePath() => DatabasePath;

    public async Task<DatabaseSummary> GetSummaryAsync()
    {
        await InitializeAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        return new DatabaseSummary(
            await CountAsync(connection, "teams"),
            await CountAsync(connection, "matches"),
            await CountAsync(connection, "asset_files"),
            await ReadBuildTimestampSettingAsync(connection) ?? "");
    }

    public async Task<string> GetBuildTimestampTextAsync()
    {
        await InitializeAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        return FormatSettingTimestamp(await ReadBuildTimestampSettingAsync(connection));
    }

    public async Task<ScheduleFetcher.FetchAllResult> GetScheduleAsync()
    {
        await InitializeAsync();
        var div1 = new List<ScheduleFetcher.Item>();
        var div2 = new List<ScheduleFetcher.Item>();
        var div3 = new List<ScheduleFetcher.Item>();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(d.division_code, ''),
                   COALESCE(m.stage_name, m.round_name, ''),
                   COALESCE(m.match_date, ''),
                   COALESCE(m.kickoff_time, ''),
                   COALESCE(m.conference, ''),
                   COALESCE(m.home_team_text, ht.team_name, m.home_seed_text, ''),
                   COALESCE(m.away_team_text, at.team_name, m.away_seed_text, ''),
                   COALESCE(m.pref, ''),
                   COALESCE(m.venue, ''),
                   m.home_score,
                   m.away_score,
                   COALESCE(m.status, ''),
                   COALESCE(m.match_info_url, ''),
                   COALESCE(m.report_url, '')
            FROM matches m
            LEFT JOIN divisions d ON d.id = m.division_id
            LEFT JOIN teams ht ON ht.id = m.home_team_id
            LEFT JOIN teams at ON at.id = m.away_team_id
            WHERE COALESCE(m.home_team_text, ht.team_name, m.home_seed_text, '') <> ''
              AND COALESCE(m.away_team_text, at.team_name, m.away_seed_text, '') <> ''
              AND (COALESCE(m.match_date, '') <> '' OR COALESCE(m.stage_name, m.round_name, '') <> '')
            ORDER BY m.match_date, m.kickoff_time, m.id
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new ScheduleFetcher.Item
            {
                Division = NormalizeDivisionCode(reader.GetString(0)),
                Round = reader.GetString(1),
                Date = FormatMatchDate(reader.GetString(2)),
                Kickoff = reader.GetString(3),
                Conference = reader.GetString(4),
                Home = reader.GetString(5),
                Away = reader.GetString(6),
                Pref = reader.GetString(7),
                Venue = reader.GetString(8),
                HomeScore = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                AwayScore = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                Status = reader.GetString(11),
                MatchInfoUrl = reader.GetString(12),
                ReportUrl = reader.GetString(13)
            };

            switch (item.Division)
            {
                case "DIV2":
                    div2.Add(item);
                    break;
                case "DIV3":
                    div3.Add(item);
                    break;
                default:
                    div1.Add(item);
                    break;
            }
        }

        return new ScheduleFetcher.FetchAllResult
        {
            Div1 = div1,
            Div2 = div2,
            Div3 = div3
        };
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ReadSettingAsync(SqliteConnection connection, string key)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_settings WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        return (await command.ExecuteScalarAsync()) as string;
    }

    private static async Task<string?> ReadBuildTimestampSettingAsync(SqliteConnection connection)
    {
        return await ReadSettingAsync(connection, "last_full_build_at")
            ?? await ReadSettingAsync(connection, "generated_at")
            ?? await ReadSettingAsync(connection, "last_generated_at");
    }

    private static string FormatSettingTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            ? timestamp.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            : value.Trim();
    }

    private static SqliteConnection CreateConnection() => new($"Data Source={DatabasePath}");

    private static async Task CopyPackageFileAsync(string packagePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var source = await FileSystem.OpenAppPackageFileAsync(packagePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);
    }

    private static void ReplaceDatabase(string seedPath, string databasePath)
    {
        var backupPath = $"{databasePath}.backup-{DateTime.Now:yyyyMMddHHmmss}";
        try
        {
            if (File.Exists(databasePath))
            {
                File.Copy(databasePath, backupPath, overwrite: true);
                File.Delete(databasePath);
            }

            File.Move(seedPath, databasePath);
        }
        catch
        {
            if (!File.Exists(databasePath) && File.Exists(backupPath))
            {
                File.Copy(backupPath, databasePath, overwrite: true);
            }

            if (File.Exists(seedPath))
            {
                File.Delete(seedPath);
            }

            throw;
        }
    }

    private static bool IsSeedNewer(string seedPath, string databasePath)
    {
        var seedVersion = ReadDatabaseVersion(seedPath);
        DatabaseVersion currentVersion;
        try
        {
            currentVersion = ReadDatabaseVersion(databasePath);
        }
        catch
        {
            return true;
        }

        return seedVersion.CompareTo(currentVersion) > 0;
    }

    private static DatabaseVersion ReadDatabaseVersion(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();

        string Setting(string key)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM app_settings WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string ?? "";
        }

        return new DatabaseVersion(
            Setting("last_full_build_at"),
            Setting("builder_version"),
            Setting("database_schema_version"));
    }

    private static async Task EnsureCacheAsync(bool force)
    {
        var seedVersion = ReadDatabaseVersion(DatabasePath).ToString();
        var markerPath = Path.Combine(CacheRoot, CacheVersionFileName);
        if (!force && File.Exists(markerPath) && File.ReadAllText(markerPath).Trim() == seedVersion)
        {
            return;
        }

        await using var manifestStream = await FileSystem.OpenAppPackageFileAsync(CacheManifestFileName);
        using var reader = new StreamReader(manifestStream);
        while (await reader.ReadLineAsync() is { } packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(AppDataRoot, packagePath.Replace('/', Path.DirectorySeparatorChar));
            if (!force && File.Exists(destinationPath))
            {
                continue;
            }

            await CopyPackageFileAsync(packagePath, destinationPath);
        }

        Directory.CreateDirectory(CacheRoot);
        await File.WriteAllTextAsync(markerPath, seedVersion);
    }

    private static string NormalizeDivisionCode(string value)
    {
        return value.Replace("DIVISION ", "DIV", StringComparison.OrdinalIgnoreCase).Trim().ToUpperInvariant();
    }

    private static string FormatMatchDate(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return $"{date.Month}月{date.Day}日";
        }

        return value;
    }
}

public sealed record DatabaseSummary(int Teams, int Matches, int Assets, string GeneratedAt);

internal sealed record DatabaseVersion(string LastFullBuildAt, string BuilderVersion, string SchemaVersion) : IComparable<DatabaseVersion>
{
    public int CompareTo(DatabaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var dateCompare = CompareDate(LastFullBuildAt, other.LastFullBuildAt);
        if (dateCompare != 0)
        {
            return dateCompare;
        }

        var builderCompare = CompareDottedVersion(BuilderVersion, other.BuilderVersion);
        if (builderCompare != 0)
        {
            return builderCompare;
        }

        return CompareDottedVersion(SchemaVersion, other.SchemaVersion);
    }

    public override string ToString() => $"{LastFullBuildAt}|{BuilderVersion}|{SchemaVersion}";

    private static int CompareDate(string left, string right)
    {
        var hasLeft = DateTimeOffset.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var leftDate);
        var hasRight = DateTimeOffset.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rightDate);
        return (hasLeft, hasRight) switch
        {
            (true, true) => leftDate.CompareTo(rightDate),
            (true, false) => 1,
            (false, true) => -1,
            _ => 0
        };
    }

    private static int CompareDottedVersion(string left, string right)
    {
        var leftParts = ParseVersion(left);
        var rightParts = ParseVersion(right);
        var count = Math.Max(leftParts.Length, rightParts.Length);
        for (var i = 0; i < count; i++)
        {
            var l = i < leftParts.Length ? leftParts[i] : 0;
            var r = i < rightParts.Length ? rightParts[i] : 0;
            if (l != r)
            {
                return l.CompareTo(r);
            }
        }

        return 0;
    }

    private static int[] ParseVersion(string value)
    {
        return value
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .ToArray();
    }
}
