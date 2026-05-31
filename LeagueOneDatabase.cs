using Microsoft.Data.Sqlite;

namespace OneRugbyNavi2;

public sealed class LeagueOneDatabase
{
    public const string DatabaseFileName = "leagueone.db";
    public const string SeedDatabaseFileName = "leagueone_seed.db";

    public static string AppDataRoot { get; } = Path.Combine(FileSystem.AppDataDirectory, "OneRugbyNavi2");

    private static string DatabasePath => Path.Combine(AppDataRoot, DatabaseFileName);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(AppDataRoot);
        if (File.Exists(DatabasePath))
        {
            return;
        }

        await using var seed = await FileSystem.OpenAppPackageFileAsync(SeedDatabaseFileName);
        await using var destination = File.Create(DatabasePath);
        await seed.CopyToAsync(destination);
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
            await ReadSettingAsync(connection, "generated_at") ?? await ReadSettingAsync(connection, "last_generated_at") ?? "");
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

    public async Task<IReadOnlyList<PlayerCard>> GetPlayersAsync(string? keyword = null, string sort = "name")
    {
        await InitializeAsync();
        var results = new List<PlayerCard>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        var where = "";
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            command.Parameters.AddWithValue("$keyword", $"%{SearchNormalizer.Normalize(keyword)}%");
            where = "WHERE psr.school_team_history_search_text LIKE $keyword";
        }

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
            SELECT p.id, p.league_one_player_id, p.name_ja, COALESCE(p.name_en, ''),
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

    public async Task<PlayerCard?> GetPlayerAsync(int playerId)
    {
        await InitializeAsync();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.league_one_player_id, p.name_ja, COALESCE(p.name_en, ''),
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

    public async Task<IReadOnlyList<RankingRow>> GetRankingAsync(string rankingType)
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
            "weight" => PlayerRankingSql("psr.weight_kg", "kg"),
            "age" => PlayerRankingSql("psr.age_calculated", "歳"),
            "caps" => PlayerRankingSql("psr.league_one_caps", "Caps"),
            _ => PlayerRankingSql("psr.height_cm", "cm")
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
                    Title = reader.GetString(0),
                    Subtitle = reader.GetString(1),
                    ValueText = reader.GetString(2)
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

    private static string PlayerRankingSql(string column, string unit) => $"""
        SELECT p.name_ja, COALESCE(t.team_name, ''), CAST({column} AS TEXT) || '{unit}'
        FROM players p
        JOIN player_season_registrations psr ON psr.player_id = p.id
        LEFT JOIN teams t ON t.id = psr.team_id
        WHERE {column} IS NOT NULL
        ORDER BY {column} DESC, p.name_ja
        LIMIT 50
        """;

    private static PlayerCard ReadPlayer(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        LeagueOnePlayerId = reader.GetString(1),
        NameJa = reader.GetString(2),
        NameEn = reader.GetString(3),
        TeamName = reader.GetString(4),
        PositionCode = reader.GetString(5),
        HeightCm = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        WeightKg = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        BirthDate = reader.GetString(8),
        AgeCalculated = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        RegistrationCategory = reader.GetString(10),
        LeagueOneCaps = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        SchoolTeamHistoryText = reader.GetString(12),
        ProfileUrl = reader.GetString(13),
        LocalAssetPath = reader.IsDBNull(14) ? null : reader.GetString(14)
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

    private static SqliteConnection CreateConnection() => new($"Data Source={DatabasePath}");
}

public sealed record DatabaseSummary(int Teams, int Players, int Matches, int Assets, string GeneratedAt);
