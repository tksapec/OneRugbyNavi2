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
            await CountAsync(connection, "players"),
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

    public async Task<IReadOnlyList<TeamCard>> GetTeamsAsync()
    {
        await InitializeAsync();
        var results = new List<TeamCard>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id, t.league_one_team_id, t.team_name, COALESCE(t.short_name, ''),
                   COALESCE(d.division_code, ''), COALESCE(t.area_text, ''), t.team_url,
                   af.local_path
            FROM teams t
            LEFT JOIN divisions d ON d.id = t.division_id
            LEFT JOIN asset_files af ON af.id = t.logo_asset_id
            ORDER BY d.division_code, t.team_name
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new TeamCard
            {
                Id = reader.GetInt32(0),
                LeagueOneTeamId = reader.GetString(1),
                TeamName = reader.GetString(2),
                ShortName = reader.GetString(3),
                DivisionCode = reader.GetString(4),
                AreaText = reader.GetString(5),
                TeamUrl = reader.GetString(6),
                LocalAssetPath = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<PlayerCard>> GetPlayersAsync(
        string? keyword = null,
        string sort = "name",
        int? teamId = null,
        string? position = null,
        string? schoolKeyword = null)
    {
        await InitializeAsync();
        var results = new List<PlayerCard>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            command.Parameters.AddWithValue("$keyword", $"%{keyword.Trim()}%");
            command.Parameters.AddWithValue("$normalizedKeyword", $"%{SearchNormalizer.Normalize(keyword)}%");
            filters.Add("""
                (p.name_ja LIKE $keyword
                 OR p.name_en LIKE $keyword
                 OR t.team_name LIKE $keyword
                 OR psr.position_code LIKE $keyword
                 OR psr.school_team_history_text LIKE $keyword
                 OR psr.school_team_history_search_text LIKE $normalizedKeyword)
                """);
        }

        if (teamId.HasValue)
        {
            command.Parameters.AddWithValue("$teamId", teamId.Value);
            filters.Add("psr.team_id = $teamId");
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            command.Parameters.AddWithValue("$position", position.Trim());
            filters.Add("psr.position_code = $position");
        }

        if (!string.IsNullOrWhiteSpace(schoolKeyword))
        {
            command.Parameters.AddWithValue("$schoolKeyword", $"%{schoolKeyword.Trim()}%");
            command.Parameters.AddWithValue("$normalizedSchoolKeyword", $"%{SearchNormalizer.Normalize(schoolKeyword)}%");
            filters.Add("""
                (psr.school_team_history_text LIKE $schoolKeyword
                 OR psr.school_team_history_search_text LIKE $normalizedSchoolKeyword)
                """);
        }

        var where = filters.Count == 0 ? "" : $"WHERE {string.Join(" AND ", filters)}";

        var orderBy = sort switch
        {
            "team" => "t.team_name, p.name_ja",
            "position" => "psr.position_code, p.name_ja",
            "height_desc" => "psr.height_cm DESC, p.name_ja",
            "weight_desc" => "psr.weight_kg DESC, p.name_ja",
            "age_desc" => "psr.age_calculated DESC, p.name_ja",
            "caps_desc" => "psr.league_one_caps DESC, p.name_ja",
            _ => "p.name_ja"
        };

        command.CommandText = $"""
            SELECT p.id, psr.team_id, p.league_one_player_id, p.name_ja, COALESCE(p.name_en, ''),
                   COALESCE(t.team_name, ''), COALESCE(psr.position_code, ''),
                   psr.height_cm, psr.weight_kg, COALESCE(p.birth_date, ''),
                   psr.age_calculated, COALESCE(psr.registration_category, ''),
                   psr.league_one_caps, COALESCE(psr.school_team_history_text, ''),
                   p.profile_url, af.local_path
            FROM players p
            JOIN player_season_registrations psr ON psr.player_id = p.id
            LEFT JOIN teams t ON t.id = psr.team_id
            LEFT JOIN asset_files af ON af.id = psr.photo_asset_id
            {where}
            ORDER BY {orderBy}
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadPlayer(reader));
        }

        return results;
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

    public async Task<PlayerCard?> GetPlayerAsync(int playerId)
    {
        await InitializeAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, psr.team_id, p.league_one_player_id, p.name_ja, COALESCE(p.name_en, ''),
                   COALESCE(t.team_name, ''), COALESCE(psr.position_code, ''),
                   psr.height_cm, psr.weight_kg, COALESCE(p.birth_date, ''),
                   psr.age_calculated, COALESCE(psr.registration_category, ''),
                   psr.league_one_caps, COALESCE(psr.school_team_history_text, ''),
                   p.profile_url, af.local_path
            FROM players p
            JOIN player_season_registrations psr ON psr.player_id = p.id
            LEFT JOIN teams t ON t.id = psr.team_id
            LEFT JOIN asset_files af ON af.id = psr.photo_asset_id
            WHERE p.id = $playerId
            """;
        command.Parameters.AddWithValue("$playerId", playerId);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadPlayer(reader) : null;
    }

    public async Task<IReadOnlyList<string>> GetPlayerPositionsAsync()
    {
        await InitializeAsync();
        var results = new List<string>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT position_code
            FROM player_season_registrations
            WHERE COALESCE(position_code, '') <> ''
            ORDER BY position_code
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    public async Task<IReadOnlyList<RankingRow>> GetRankingAsync(string rankingType, bool descending = true)
    {
        await InitializeAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = rankingType switch
        {
            "school_count" => """
                SELECT candidate_text, candidate_type, player_count
                FROM school_candidates
                ORDER BY player_count DESC, candidate_text
                LIMIT 50
                """,
            "weight" => PlayerRankingSql("psr.weight_kg", "kg", descending),
            "age" => PlayerRankingSql("psr.age_calculated", "豁ｳ", descending),
            "caps" => PlayerRankingSql("psr.league_one_caps", "Caps", descending),
            _ => PlayerRankingSql("psr.height_cm", "cm", descending)
        };

        var rows = new List<RankingRow>();
        await using var reader = await command.ExecuteReaderAsync();
        var rank = 1;
        while (await reader.ReadAsync())
        {
            if (rankingType == "school_count")
            {
                rows.Add(new RankingRow
                {
                    Rank = rank++,
                    Title = reader.GetString(0),
                    Subtitle = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ValueText = $"{reader.GetInt32(2)}人"
                });
            }
            else
            {
                rows.Add(new RankingRow
                {
                    Rank = rank++,
                    PlayerId = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Subtitle = reader.GetString(2),
                    ValueText = reader.GetString(3),
                    LocalAssetPath = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
        }

        return rows;
    }

    public async Task<int> CountPlayersForTeamAsync(int teamId)
    {
        await InitializeAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM player_season_registrations WHERE team_id = $teamId";
        command.Parameters.AddWithValue("$teamId", teamId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string PlayerRankingSql(string column, string unit, bool descending) => $"""
        SELECT p.id, p.name_ja, COALESCE(t.team_name, '') || ' / ' || COALESCE(psr.position_code, ''),
               CAST({column} AS TEXT) || '{unit}', af.local_path
        FROM players p
        JOIN player_season_registrations psr ON psr.player_id = p.id
        LEFT JOIN teams t ON t.id = psr.team_id
        LEFT JOIN asset_files af ON af.id = psr.photo_asset_id
        WHERE {column} IS NOT NULL
        ORDER BY {column} {(descending ? "DESC" : "ASC")}, p.name_ja
        LIMIT 50
        """;

    private static PlayerCard ReadPlayer(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        TeamId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
        LeagueOnePlayerId = reader.GetString(2),
        NameJa = reader.GetString(3),
        NameEn = reader.GetString(4),
        TeamName = reader.GetString(5),
        PositionCode = reader.GetString(6),
        HeightCm = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        WeightKg = reader.IsDBNull(8) ? null : reader.GetInt32(8),
        BirthDate = reader.GetString(9),
        AgeCalculated = reader.IsDBNull(10) ? null : reader.GetInt32(10),
        RegistrationCategory = reader.GetString(11),
        LeagueOneCaps = reader.IsDBNull(12) ? null : reader.GetInt32(12),
        SchoolTeamHistoryText = reader.GetString(13),
        ProfileUrl = reader.GetString(14),
        LocalAssetPath = reader.IsDBNull(15) ? null : reader.GetString(15)
    };

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

public sealed record DatabaseSummary(int Teams, int Players, int Matches, int Assets, string GeneratedAt);

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
