# 2026-27 Player and Team Synchronization Design

> **Scope update (2026-10-02):** The player roster/profile synchronization and app-owned player directory, player detail, school search, and local-stat ranking features in this historical design are no longer part of the current app scope. Keep the current official team catalog, and show League One's official points/tries/other rankings inside an in-app WebView at `/ranking/?year=...`; do not synthesize values when the official page says data is unavailable.

## Goal

Improve team and player data acquisition so OneRugbyNavi2 can adopt the 2026-27 season safely even while the official League One team/player pages are still partly on 2025-26, and continue to work for future season transitions without guessing unpublished roster data.

## Approved product behavior

- On app startup, refresh only lightweight team catalog data when the cache is stale.
- Full player/profile/photo acquisition runs only from an explicit data update action.
- Official League One pages are the source of truth. News releases are not used to synthesize a roster.
- A page that is still labeled for an older season is a normal `not published yet` state, not an update failure.
- Existing database rows and cached images remain available when a current-season source is not published or an individual request fails.
- 2026-27 data can become available team by team. A single stale/failed team must not block other current-season teams.

## Source hierarchy

### Team catalog

Primary current-season source for team IDs, official names, division membership and logo URLs:

- `https://league-one.jp/team/` (current page title and internal `/team/{id}` links)

The season-specific content page is a second source for the public Division list and club presentation:

- `https://league-one.jp/content/team/{seasonStartYear}`

It supplies Division membership, displayed names, club website links and logo URLs, but its club links are external and do not supply League One team IDs. Keep each page's original team label; use a separate verified identity alias only when matching data from different official pages.

Supplemental current metadata may be read from League One's public team/about pages only when it can be matched unambiguously to a current catalog entry. Supplemental metadata must never override the season membership from `content/team/{seasonStartYear}`.

### Team roster index and team pages

- `https://league-one.jp/team/`
- discovered internal team links such as `/team/116`

These pages are eligible for roster synchronization only when the rendered page text identifies the requested season (for 2026, `2026-27シーズン`). A 2025-26 page must not create 2026-27 registrations.

### Player detail pages

- discovered `/player/{id}` links from a current-season team roster page

A player detail page is used to enrich a current-season registration only when its page season matches the target season. If it remains on the previous season, the current team-list row may still be used for fields explicitly present in that current-season roster row, while stale detail-only fields are not copied forward as if they were current.

## Readiness model

Every fetched page has a semantic readiness state:

- `Ready`: target season label is present and required structure parsed.
- `NotPublished`: page is valid but explicitly belongs to another season.
- `Partial`: target season is correct but optional fields or some children failed.
- `Failed`: transport or required parsing failed.

`NotPublished` never deletes or overwrites existing current/previous data.

## Team catalog cache

Create an app-data JSON cache per season containing:

- target season start year
- fetched timestamp
- source URL
- team name
- normalized team name
- division code
- optional short name / area / logo URL when safely known

The cache is refreshed at startup only when stale. Default staleness interval: 12 hours. Startup refresh is non-blocking and exceptions are contained.

The Team screen fetches the current official catalog when opened, with an 8-second request timeout and a 24-hour JSON cache. It falls back to a validated cache or season-specific catalog when offline. Current official logos are fetched only while the Team screen is open, through a bounded image cache that validates file signatures and writes by hash through a temporary file. A renamed club does not reuse a legacy logo when no current logo is available. No roster, player page or player photo request runs at app startup.

## Full data update flow

1. Acquire a single update gate.
2. Initialize the application database.
3. Refresh the current team catalog.
4. Fetch `/team/` and determine the site's roster season.
5. If `/team/` is still an older season, return `NotPublished` without writing the database.
6. Copy `leagueone.db` to a temporary update database.
7. Create/resolve the target season and divisions in the temporary database.
8. For each current catalog team:
   - resolve its internal League One team page from the current-season team index;
   - fetch and validate the team page season;
   - parse roster rows;
   - upsert the target-season team row;
   - for each roster player, upsert player identity and the target-season registration;
   - fetch the player detail page and enrich only when its season matches;
   - download/update the player photo independently; an image failure does not roll back player metadata;
   - record page/result diagnostics.
9. Commit each team's SQLite work transaction only after that team's required data parsed successfully.
10. Validate the temporary database with integrity checks and basic referential/count sanity checks.
11. Replace the live database atomically only if the temporary database remains valid. Keep a backup for restoration on replacement failure.
12. Return counts for updated teams, players, new players, missing roster candidates, image updates, not-published teams and failed pages.

A team missing from a successfully parsed current-season roster is not deleted from `players`. Its old `player_season_registrations` row remains historical. No new registration is created for that player for the target season.

## SQLite behavior

The existing documented schema remains authoritative. Runtime code must validate required tables/columns with `PRAGMA table_info` before attempting writes. If the packaged DB schema does not match the expected contract, the update is aborted before live DB replacement.

Writes use parameterized SQL. Do not depend on undocumented UNIQUE indexes: resolve IDs first, then update or insert explicitly.

Season history is preserved. New `teams` and `player_season_registrations` rows are associated with the target `season_id`; previous seasons are not overwritten.

Read queries are season-scoped to avoid duplicate cross-season display:

- Team display uses the requested/current season; it does not silently substitute the latest older season.
- Team-specific player display uses the selected/current season's registration only.
- Global player/ranking/search display uses the requested/current season's registration only. Older registrations remain in SQLite and can be queried by an explicit season.

The UI must expose freshness so users can distinguish current 2026-27 data from a historical fallback during the publication transition.

## Images

Player images are downloaded to a temporary file under the existing app cache hierarchy and renamed into place only after a successful download. The existing cached image stays untouched on failure.

`asset_files` is updated only after the local file is safely available. Hashing is used to avoid unnecessary rewrites when practical.

Team logo download is optional during lightweight startup refresh; no startup operation should download the full player image set.

## Parser design

Parsing is split from networking so it can be unit tested with fixture HTML.

- `LeagueOneSeasonDetector`: detects a concrete season label and compares it to a requested start year.
- `TeamCatalogParser`: parses Division membership and team names from `content/team/{year}`.
- `TeamIndexParser`: parses current-season internal League One team links.
- `TeamRosterParser`: parses roster rows, player links and row-level fields.
- `PlayerProfileParser`: parses English name, position, dimensions, birth date, school/team history, registration category and League One caps from a season-validated player page.

Selectors prefer semantic text/links and table headers over a single fragile CSS class. All text is HTML-decoded and whitespace-normalized.

## Error handling and safety

- HTTP uses timeouts and a League One URL allow-list for internal detail links.
- Redirect/result URLs outside approved HTTP(S) hosts are not written as source URLs for League One detail pages.
- Cancellation is supported for full updates.
- One player detail/photo failure degrades that player to row-level data and records a failure; it does not corrupt the team transaction.
- A team page with the wrong season is skipped with `NotPublished`.
- A team page with malformed required roster structure is `Failed` and its existing data is preserved.
- Live database replacement occurs only after temporary DB validation.

## UI changes

Team page:

- Show a compact data freshness/status line.
- Do not expose the existing update-preparation placeholder as a real update action. It stays hidden until a complete, season-safe roster update path is implemented.
- Report missing current-season registrations clearly; do not display old-season players as current.

The current hidden `更新準備確認` placeholder is removed/replaced; user-facing text must no longer claim that real updating is unimplemented after the implementation lands.

## Testing and verification

Add a pure .NET test project for parser and database-sync core behavior so tests do not require an Android runtime. Tests cover at minimum:

- current 2026-27 season detection;
- rejecting a 2025-26 team/player page when target is 2026;
- parsing team catalog Division membership;
- parsing a team roster row including player URL, position, size, birth date and arbitrary registration category text;
- parsing player profile fields;
- preserving an old registration when the new season is not published;
- upserting a new season without creating duplicate player identities;
- no duplicate player display when multiple seasons exist;
- safe handling of partial detail/photo failures.

Do not add or run GitHub Actions. Run tests and builds locally when a .NET SDK and required MAUI workloads are available; otherwise report them as unverified. A missing local SDK is not a reason to introduce a workflow.

## Non-goals

- Do not infer the 2026-27 full roster from club news releases.
- Do not delete previous-season player/team history.
- Do not fabricate League One team/player IDs for pages that have not been published.
- Do not force reuse of old logos for teams whose branding changed.
