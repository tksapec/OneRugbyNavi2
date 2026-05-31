PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS seasons (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    season_code TEXT NOT NULL UNIQUE,
    season_year INTEGER NOT NULL,
    source_url TEXT,
    fetched_at TEXT
);

CREATE TABLE IF NOT EXISTS divisions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    season_id INTEGER NOT NULL,
    division_name TEXT NOT NULL,
    division_code TEXT NOT NULL,
    conference TEXT,
    FOREIGN KEY (season_id) REFERENCES seasons(id),
    UNIQUE(season_id, division_code, conference)
);

CREATE TABLE IF NOT EXISTS asset_files (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    asset_type TEXT NOT NULL,
    related_type TEXT NOT NULL,
    related_id INTEGER,
    source_url TEXT,
    local_path TEXT,
    file_name TEXT,
    content_hash TEXT,
    downloaded_at TEXT,
    last_checked_at TEXT,
    is_available INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS teams (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    league_one_team_id TEXT NOT NULL,
    season_id INTEGER NOT NULL,
    division_id INTEGER NOT NULL,
    team_name TEXT NOT NULL,
    short_name TEXT,
    area_text TEXT,
    team_url TEXT NOT NULL,
    logo_asset_id INTEGER,
    raw_json TEXT,
    fetched_at TEXT,
    FOREIGN KEY (season_id) REFERENCES seasons(id),
    FOREIGN KEY (division_id) REFERENCES divisions(id),
    FOREIGN KEY (logo_asset_id) REFERENCES asset_files(id),
    UNIQUE(season_id, league_one_team_id)
);

CREATE TABLE IF NOT EXISTS team_profiles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    team_id INTEGER NOT NULL,
    organization_name TEXT,
    organization_address TEXT,
    official_team_name TEXT,
    display_name TEXT,
    host_area TEXT,
    secondary_host_area TEXT,
    training_ground_address TEXT,
    official_site_url TEXT,
    raw_json TEXT,
    fetched_at TEXT,
    FOREIGN KEY (team_id) REFERENCES teams(id),
    UNIQUE(team_id)
);

CREATE TABLE IF NOT EXISTS team_stadiums (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    team_id INTEGER NOT NULL,
    stadium_name TEXT NOT NULL,
    stadium_url TEXT,
    raw_json TEXT,
    fetched_at TEXT,
    FOREIGN KEY (team_id) REFERENCES teams(id)
);

CREATE TABLE IF NOT EXISTS matches (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    season_id INTEGER NOT NULL,
    division_id INTEGER,
    match_category TEXT NOT NULL DEFAULT 'Unknown',
    stage_name TEXT,
    round_name TEXT,
    match_date TEXT,
    kickoff_time TEXT,
    conference TEXT,
    home_team_id INTEGER,
    away_team_id INTEGER,
    home_team_text TEXT,
    away_team_text TEXT,
    home_seed_text TEXT,
    away_seed_text TEXT,
    has_undetermined_team INTEGER NOT NULL DEFAULT 0,
    pref TEXT,
    venue TEXT,
    home_score INTEGER,
    away_score INTEGER,
    status TEXT,
    match_info_url TEXT,
    report_url TEXT,
    source_url TEXT,
    raw_json TEXT,
    fetched_at TEXT,
    FOREIGN KEY (season_id) REFERENCES seasons(id),
    FOREIGN KEY (division_id) REFERENCES divisions(id),
    FOREIGN KEY (home_team_id) REFERENCES teams(id),
    FOREIGN KEY (away_team_id) REFERENCES teams(id)
);

CREATE TABLE IF NOT EXISTS players (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    league_one_player_id TEXT NOT NULL UNIQUE,
    name_ja TEXT NOT NULL,
    name_en TEXT,
    birth_date TEXT,
    profile_url TEXT NOT NULL,
    raw_json TEXT,
    created_at TEXT,
    updated_at TEXT
);

CREATE TABLE IF NOT EXISTS player_season_registrations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id INTEGER NOT NULL,
    season_id INTEGER NOT NULL,
    team_id INTEGER,
    division_id INTEGER,
    position_code TEXT,
    position_name TEXT,
    height_cm INTEGER,
    weight_kg INTEGER,
    age_displayed INTEGER,
    age_calculated INTEGER,
    registration_category TEXT,
    league_one_caps INTEGER,
    school_team_history_text TEXT,
    school_team_history_search_text TEXT,
    photo_asset_id INTEGER,
    raw_json TEXT,
    fetched_at TEXT,
    FOREIGN KEY (player_id) REFERENCES players(id),
    FOREIGN KEY (season_id) REFERENCES seasons(id),
    FOREIGN KEY (team_id) REFERENCES teams(id),
    FOREIGN KEY (division_id) REFERENCES divisions(id),
    FOREIGN KEY (photo_asset_id) REFERENCES asset_files(id),
    UNIQUE(player_id, season_id)
);

CREATE TABLE IF NOT EXISTS player_school_team_histories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id INTEGER NOT NULL,
    season_id INTEGER NOT NULL,
    raw_text TEXT,
    item_order INTEGER,
    item_name TEXT,
    normalized_name TEXT,
    item_type TEXT,
    confidence REAL,
    FOREIGN KEY (player_id) REFERENCES players(id),
    FOREIGN KEY (season_id) REFERENCES seasons(id)
);

CREATE TABLE IF NOT EXISTS player_match_stats (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id INTEGER NOT NULL,
    season_id INTEGER NOT NULL,
    match_id INTEGER,
    competition_name TEXT,
    division_name TEXT,
    round_name TEXT,
    match_date TEXT,
    opponent_team_name TEXT,
    score_text TEXT,
    p INTEGER,
    t INTEGER,
    g INTEGER,
    pg INTEGER,
    dg INTEGER,
    success_rate_text TEXT,
    success_rate REAL,
    match_url TEXT,
    raw_json TEXT,
    FOREIGN KEY (player_id) REFERENCES players(id),
    FOREIGN KEY (season_id) REFERENCES seasons(id),
    FOREIGN KEY (match_id) REFERENCES matches(id)
);

CREATE TABLE IF NOT EXISTS school_candidates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    season_id INTEGER NOT NULL,
    candidate_text TEXT NOT NULL,
    normalized_text TEXT NOT NULL,
    candidate_type TEXT,
    player_count INTEGER NOT NULL DEFAULT 0,
    confidence REAL,
    last_updated_at TEXT,
    FOREIGN KEY (season_id) REFERENCES seasons(id),
    UNIQUE(season_id, normalized_text)
);

CREATE TABLE IF NOT EXISTS search_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    search_type TEXT NOT NULL,
    keyword TEXT NOT NULL,
    normalized_keyword TEXT NOT NULL,
    hit_count INTEGER,
    searched_at TEXT
);

CREATE TABLE IF NOT EXISTS source_pages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    crawl_run_id INTEGER,
    source_type TEXT NOT NULL,
    url TEXT NOT NULL,
    http_status INTEGER,
    content_hash TEXT,
    fetched_at TEXT,
    parse_status TEXT,
    error_message TEXT,
    raw_html_path TEXT
);

CREATE TABLE IF NOT EXISTS crawl_runs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    started_at TEXT,
    finished_at TEXT,
    target_season TEXT,
    status TEXT,
    total_team_count INTEGER DEFAULT 0,
    total_player_count INTEGER DEFAULT 0,
    total_match_count INTEGER DEFAULT 0,
    error_count INTEGER DEFAULT 0,
    note TEXT
);

CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT,
    updated_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_teams_season_division ON teams(season_id, division_id);
CREATE INDEX IF NOT EXISTS idx_matches_season_category ON matches(season_id, match_category);
CREATE INDEX IF NOT EXISTS idx_players_name_ja ON players(name_ja);
CREATE INDEX IF NOT EXISTS idx_player_reg_season_team ON player_season_registrations(season_id, team_id);
CREATE INDEX IF NOT EXISTS idx_player_reg_position ON player_season_registrations(position_code);
CREATE INDEX IF NOT EXISTS idx_player_reg_school_search ON player_season_registrations(school_team_history_search_text);
CREATE INDEX IF NOT EXISTS idx_school_candidates_normalized ON school_candidates(normalized_text);
CREATE INDEX IF NOT EXISTS idx_search_history_type_time ON search_history(search_type, searched_at);
CREATE INDEX IF NOT EXISTS idx_source_pages_url ON source_pages(url);
