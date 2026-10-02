# Player and Team Synchronization Implementation Plan

> **Scope update (2026-10-02):** This remains a historical implementation plan. Do not restore roster/profile crawlers or app-owned player directory, player detail, school search, or local player-stat rankings. Current player-related UI is limited to the official League One ranking page embedded in the app.

> **Execution note (2026-10-01):** This earlier draft is not an active runbook as written. The current authorized work uses `main` only, creates no branch/worktree, and must not add or run GitHub Actions. The official pages were rechecked live; `https://league-one.jp/team/` currently provides the 2026-27 team index, while sampled 2026-27 team pages have empty rosters and older player pages remain labeled 2025-26. Do not execute a step below that conflicts with those facts or the current user instructions. The safe team catalog and season-scoped read changes are implemented separately; full roster synchronization and a seed DB rebuild remain blocked until current-season roster rows are published.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Safely synchronize current-season League One team and player data while the official site may still expose prior-season roster pages.

**Architecture:** Keep lightweight team-catalog acquisition separate from full roster/profile synchronization. Parse and season-validate official HTML before writing; write full updates into a temporary SQLite copy, preserve historical rows, validate the copy, then atomically replace the live database.

**Tech Stack:** .NET 9, .NET MAUI, C#, HtmlAgilityPack 1.12.2, Microsoft.Data.Sqlite 9.0.5, System.Text.Json, xUnit. GitHub Actions are excluded by the active instructions.

**Spec:** `docs/superpowers/specs/2026-09-09-player-team-sync-design.md`

## Global Constraints

- Use `main` only. Do not create another branch or worktree branch.
- Official League One pages are the source of truth; do not synthesize complete rosters from news releases.
- A page explicitly showing another season is `NotPublished` and must not overwrite existing data.
- Preserve previous-season rows and cached images.
- Use parameterized SQL for values and validate required schema before writes.
- Startup work downloads team catalog metadata only; full player/profile/photo downloads require explicit user action.
- Do not add or run GitHub Actions. If local `dotnet` is unavailable, report tests/build as unverified.

---

### Task 1: Core test harness and RED parser contract

**Files:**
- Create: `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj`
- Create: `tests/OneRugbyNavi2.Core.Tests/SeasonAndCatalogParserTests.cs`

**Interfaces:**
- Consumes: none.
- Produces: test contract for `LeagueOneSeasonDetector.Detect(string)` and `TeamCatalogParser.Parse(string, int, string)`.

- [ ] **Step 1: Create a pure net9.0 xUnit project**

Use `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and `HtmlAgilityPack`. Link production parser/model files with `<Compile Include="../../LeagueOneSyncModels.cs" Link="Production/LeagueOneSyncModels.cs" />`, `LeagueOneSeasonDetector.cs`, and `TeamCatalogParser.cs` so the tests remain independent of the Android MAUI target.

- [ ] **Step 2: Write failing tests**

Tests must assert:

```csharp
Assert.Equal(2026, LeagueOneSeasonDetector.Detect("<h2>2026-27シーズン</h2>"));
Assert.Equal(2025, LeagueOneSeasonDetector.Detect("<h2>2025-26シーズン</h2>"));
```

and parse a minimal HTML sample containing `DIVISION 1`, `DIVISION 2`, `DIVISION 3` with team names into `TeamCatalogEntry` objects with `DIV1`, `DIV2`, `DIV3`.

- [x] **Step 3: Exclude GitHub Actions**

Do not create `.github/workflows/core-tests.yml`; this step is superseded by the active no-GitHub-Actions instruction.

- [ ] **Step 4: Verify RED locally**

Run the test project locally when `dotnet` is installed. Do not push a test-only commit to trigger CI. If the SDK is unavailable, report the RED state as unverified.

- [ ] **Step 5: Commit**

Commit message: `test: define season and team catalog parser contract`.

---

### Task 2: Season detection and team catalog parsing

**Files:**
- Create: `LeagueOneSyncModels.cs`
- Create: `LeagueOneSeasonDetector.cs`
- Create: `TeamCatalogParser.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/SeasonAndCatalogParserTests.cs`

**Interfaces:**
- Produces:
  - `enum SyncReadiness { Ready, NotPublished, Partial, Failed }`
  - `sealed record TeamCatalogEntry(string TeamName, string DivisionCode, string ShortName, string AreaText, string LogoUrl)`
  - `sealed record TeamCatalogSnapshot(int SeasonStartYear, DateTimeOffset FetchedAt, string SourceUrl, IReadOnlyList<TeamCatalogEntry> Teams)`
  - `static int LeagueOneSeasonDetector.Detect(string html)` returning season start year or `0`.
  - `static IReadOnlyList<TeamCatalogEntry> TeamCatalogParser.Parse(string html, int seasonStartYear, string sourceUrl)`.

- [ ] **Step 1: Implement minimal models and detector**

Season detection recognizes normalized forms such as `2026-27シーズン`, `2026ｰ27`, and `2026‐27`, returning the first year.

- [ ] **Step 2: Implement catalog parser**

Parse headings/text in DOM order, switch current division on `DIVISION 1/2/3`, then collect team display text from current section while rejecting navigation/footer noise. Normalize with `SeasonCatalog.NormalizeTeamName` at call sites, not inside the generic parser.

- [ ] **Step 3: Add boundary tests**

Cover duplicate team nodes, empty HTML, and a stale team name value. Parser output must deduplicate exact team names within the same division.

- [ ] **Step 4: Verify GREEN locally**

Run the core tests locally when `dotnet` is installed. Report the tests as unverified if the SDK is unavailable.

- [ ] **Step 5: Commit**

Commit message: `feat: parse season-safe League One team catalog`.

---

### Task 3: Team index, roster and player profile parsers

**Files:**
- Create: `TeamIndexParser.cs`
- Create: `TeamRosterParser.cs`
- Create: `PlayerProfileParser.cs`
- Create: `tests/OneRugbyNavi2.Core.Tests/RosterAndPlayerParserTests.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj`

**Interfaces:**
- Produces:
  - `TeamIndexParseResult` with readiness, detected season and `TeamPageLink` values.
  - `TeamRosterParseResult` with readiness, team metadata and `RosterPlayer` rows.
  - `PlayerProfileParseResult` with readiness and optional `PlayerProfileData`.
- `RosterPlayer` includes player ID, profile URL, display name, position code/name, height, weight, birth date, registration category, image URL.
- `PlayerProfileData` includes English name, school/team history, caps and profile-level fields.

- [ ] **Step 1: Write RED tests for wrong-season pages**

A target 2026 parse of HTML containing `2025-26シーズン` must return `SyncReadiness.NotPublished` and zero current-season roster output.

- [ ] **Step 2: Write RED tests for a current roster row**

Use representative table HTML with a `/player/483965` anchor, `PR （プロップ）`, `180cm`, `117kg`, `1994年05月27日`, and `カテゴリA-1`; assert the registration category is preserved verbatim rather than constrained to A/B/C.

- [ ] **Step 3: Write RED tests for profile enrichment**

Use representative profile HTML with `Takayuki Watanabe`, school/team history and `リーグワンキャップ数：12`; assert exact parsed values.

- [ ] **Step 4: Implement parsers**

Resolve only relative or absolute `https://league-one.jp/team/` and `https://league-one.jp/player/` URLs. Normalize whitespace and HTML entities. Parse by semantic headers/labels first and CSS classes only as secondary hints.

- [ ] **Step 5: Verify GREEN and commit**

Expected: all core parser tests pass. Commit: `feat: parse current-season team rosters and player profiles`.

---

### Task 4: Lightweight team catalog service and startup cache

**Files:**
- Create: `TeamCatalogService.cs`
- Modify: `AppServices.cs`
- Modify: `App.xaml.cs`
- Modify: `DataModels.cs`
- Modify: `TeamListPage.cs`
- Create: `tests/OneRugbyNavi2.Core.Tests/TeamCatalogServiceTests.cs` for pure cache/freshness helpers where possible.

**Interfaces:**
- `Task<TeamCatalogSnapshot> TeamCatalogService.GetForDisplayAsync(int seasonStartYear, CancellationToken cancellationToken = default)`.
- `Task RefreshIfStaleSafeAsync(CancellationToken cancellationToken = default)` catches/logically contains startup exceptions.
- Cache path: `AppData/OneRugbyNavi2/team_catalog_{year}.json`.
- Cache TTL: 12 hours.

- [ ] **Step 1: Add RED tests for cache freshness helper**

At exactly 12 hours the cache is stale; under 12 hours it is fresh.

- [ ] **Step 2: Implement HTTP fetch and JSON cache**

Use a bounded HttpClient timeout. Validate catalog has plausible division/team content before replacing cache. Write JSON to a temporary file then rename.

- [ ] **Step 3: Wire non-blocking startup refresh**

`App.CreateWindow` starts only `RefreshIfStaleSafeAsync`; no player pages/photos are touched.

- [ ] **Step 4: Merge catalog into Team screen**

Add `SeasonStartYear` to `TeamCard`. Prefer current catalog names/divisions; merge latest stored SQLite metadata by normalized current name; preserve fallback `SeasonCatalog.Teams2026` when cache/network is unavailable.

- [ ] **Step 5: Verify core tests and commit**

Commit: `feat: refresh current team catalog at startup`.

---

### Task 5: SQLite temporary-database updater and cross-season reads

**Files:**
- Create: `LeagueOneDatabaseUpdater.cs`
- Modify: `LeagueOneDatabase.cs`
- Create: `tests/OneRugbyNavi2.Core.Tests/DatabaseUpdaterTests.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj` to include `Microsoft.Data.Sqlite` and link pure updater/model helpers.

**Interfaces:**
- `LeagueOneDatabaseUpdater(string databasePath)`.
- `Task ValidateSchemaAsync()` verifies required tables/columns.
- `Task<TeamDatabaseUpdateResult> ApplyTeamAsync(int seasonStartYear, TeamCatalogEntry team, TeamRosterParseResult roster, IReadOnlyDictionary<string, PlayerProfileData> profiles, IReadOnlyDictionary<string, CachedAssetInfo> assets, CancellationToken cancellationToken)`.
- `Task ValidateDatabaseAsync()` performs `PRAGMA integrity_check`, orphan checks and count sanity.

- [ ] **Step 1: Build a minimal test SQLite schema**

Create `seasons`, `divisions`, `teams`, `players`, `player_season_registrations`, `asset_files`, `source_pages`, `crawl_runs`, `app_settings` with the documented columns needed by runtime synchronization.

- [ ] **Step 2: RED test: old data survives NotPublished**

Seed a 2025 player registration, skip `ApplyTeamAsync`, assert the old row remains unchanged.

- [ ] **Step 3: RED test: current season upsert preserves player identity**

Seed `players.league_one_player_id=483965`, apply a 2026 registration, assert one player identity and two season registrations.

- [ ] **Step 4: Implement schema validation and parameterized upserts**

Create/resolve season and divisions, then team/player/registration rows using select/update/insert rather than relying on unknown unique indexes.

- [ ] **Step 5: Fix read queries**

`GetTeamsAsync` returns season year and avoids accidental duplicate display. Global player/ranking/position queries select the newest known registration per player. Team-specific queries remain tied to the selected season-specific team ID.

- [ ] **Step 6: RED/GREEN test duplicate-display query behavior**

With 2025 and 2026 registration rows for one player, global display returns one row using 2026 values.

- [ ] **Step 7: Verify and commit**

Commit: `feat: safely upsert season-specific team and player data`.

---

### Task 6: Full explicit synchronization, photos and safe replacement

**Files:**
- Create: `LeagueOneRosterSyncService.cs`
- Create: `LeagueOneAssetDownloader.cs`
- Replace: `TeamUpdateService.cs`
- Modify: `AppServices.cs`
- Modify: `DataModels.cs`
- Create: `tests/OneRugbyNavi2.Core.Tests/SyncDecisionTests.cs`

**Interfaces:**
- `Task<TeamUpdateResult> TeamUpdateService.UpdateAllAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)`.
- Keep `UpdateTeamAsync` only if useful for targeted retry; it must perform real synchronization or be removed from UI.
- Result includes updated team/player/image counts plus `NotPublishedTeams` and `FailedPages`.

- [ ] **Step 1: RED tests for update decision rules**

Wrong-season `/team/` index => no DB replacement. Current team page + stale player detail => registration row data accepted but stale detail-only values not copied forward. Photo failure => metadata success remains possible.

- [ ] **Step 2: Implement roster/profile HTTP orchestration**

Fetch `/team/`, validate target season, discover current internal team links, then process current teams with bounded sequential/conservative parallel requests. Carry cancellation through all calls.

- [ ] **Step 3: Implement safe image download**

Download to temporary file, compute hash, then rename. Preserve existing file on failure. Only update `asset_files` after local success.

- [ ] **Step 4: Implement temporary DB copy and atomic replacement**

Copy live DB, update only temp DB, run validation, create live backup, replace live DB, restore backup if replacement throws.

- [ ] **Step 5: Record diagnostics**

Use existing `crawl_runs`/`source_pages` when schema validation confirms them. Store URL, status, parse state, error and fetched timestamp; do not store secrets.

- [ ] **Step 6: Verify and commit**

Commit: `feat: synchronize League One rosters and player profiles`.

---

### Task 7: Data update UI and freshness reporting

**Files:**
- Modify: `TeamListPage.cs`
- Modify: `PlayerDirectoryPage.cs`
- Modify: `PlayerDetailPage.cs` only if needed for freshness text.
- Modify: `DataModels.cs`

**Interfaces:**
- Team screen has visible `データ更新` action.
- `TeamUpdateResult` reports `UpdatedTeams`, `UpdatedPlayers`, `NewPlayers`, `MissingCandidates`, `UpdatedImages`, `NotPublishedTeams`, `FailedPages`, `UpdatedAt`, `Message`.

- [ ] **Step 1: Replace hidden preparation button behavior**

Remove `ShowUpdatePreparationButton` and `更新準備確認` copy. Add one page-level full update button; disable it while running.

- [ ] **Step 2: Show explicit transition states**

Examples:

```text
2026-27 チーム: 27 / 27 取得済み
2026-27 選手: 公式サイト未公開（2025-26データを表示）
```

or after partial publication:

```text
2026-27 選手: 8チーム更新済み / 19チーム未公開
```

- [ ] **Step 3: Ensure tapping a newly published team works**

If a 2026 team row now exists, navigate with its real DB ID. If the team still has no League One roster page/DB row, show the current-season-not-published message rather than claiming the team is unsupported.

- [ ] **Step 4: Review colors/layout against `PageStyles`**

No new purple/default MAUI colors; use existing semantic palette.

- [ ] **Step 5: Verify and commit**

Commit: `feat: expose safe League One data refresh in team UI`.

---

### Task 8: Review, regression checks and completion validation

**Files:**
- Review the full working diff on `main`, including the previously created season-review and player-team-sync commits before finalizing.

**Interfaces:** none.

- [ ] **Step 1: Run all core tests locally**

Do not add or run an Actions workflow. Require a successful local test run before claiming tests pass.

- [ ] **Step 2: Attempt Android MAUI build locally**

Run `dotnet build OneRugbyNavi2.csproj -f net9.0-android -c Release` only in an available local environment. If the SDK/workload is unavailable, record that as unverified; do not report a pass.

- [ ] **Step 3: Diff review**

Check spec match, stale-season safety, transaction boundaries, SQL parameterization, URL allow-list, cancellation, duplicate-season reads, image failure behavior, UI copy, and accidental unrelated changes.

- [ ] **Step 4: Use Superpowers review skills**

Use the available review workflow, fix findings and repeat local verification where the toolchain permits.

- [ ] **Step 5: Compare branch to base and report unresolved items**

Review the final diff on `main`. The active task authorizes necessary reviewed changes to be integrated into `main`; do not create another branch or trigger Actions.
