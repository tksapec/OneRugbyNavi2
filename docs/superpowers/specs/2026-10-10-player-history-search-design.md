# Player History and Search Design

Status: approved design, reviewed with RugbyDB source direction
Date: 2026-10-10

## Goal

Enrich the 2026-27 player catalog with sourced rugby affiliations and make the player search easier to understand. The catalog currently has 1,480 players and 1,617 history rows. It has 1,336 Rugby Database player-page sources and 1,065 League One sources. Dates are not recorded for 360 history rows.

## Scope

- Review all 1,480 players in `Resources/Raw/league-one-players-2026-27.json`.
- Add supported rugby affiliations for senior clubs, universities, and high schools.
- Keep representative history in `representativeHistory`.
- Keep the existing `schools` summary for player profile and search compatibility.
- Do not add runtime web scraping or a new database service. The application continues to load its bundled catalog.
- Do not infer teams or dates from gaps in a player's record. Store only affiliations supported by a source. Preserve unknown dates as unknown.

## Source policy

Use RugbyDB as the main discovery and reference source for historical team, university, high-school, and season entries, as requested. Check the season-level records rather than relying only on a profile header: a player's profile affiliation can disagree with the season table. Cite RugbyDB as a secondary source when it is the only explicit record. Cross-check current club and explicit transfer or tenure dates against official League One, club, school, or university sources when available. A record for one season supports that season only; it does not prove continuous membership in missing seasons.

Keep publisher, URL, checked date, and supported fields in the player's `sources` list. Sources supporting an affiliation list `teamHistory` in `fields`. Prefer a direct transfer or departure notice when it states a tenure range. If no source supports a period, keep the affiliation date empty and display it as unconfirmed.

For example, official League One notices identify Rintaro Maruyama's Toyota Verblitz tenure as 2022-2024 and his Hanazono Kintetsu Liners joining season as 2024-25. League One's 2024-25 and 2025-26 pages confirm his Hanazono affiliation for those seasons. Tochigi Honda Heat's 2026 player page confirms his current club. Store the Toyota stint as 2022-23 through 2023-24, Hanazono as 2024-25 through 2025-26, and Honda Heat as 2026-27 only when the season evidence is present.

## Data model

Add a `type` to each `PlayerTeamHistory` row with the values `club`, `university`, or `highSchool`. Existing rows without `type` are treated as `club` for backward compatibility. Keep existing League One team IDs for clubs. Use stable namespaced IDs such as `university:<official name>` and `highSchool:<official name>` for education teams; these IDs are identifiers only and do not imply League One membership.

Store dates using the existing `YYYY-YY` season format. A single confirmed season uses the same start and end season. An explicit multi-season tenure may use a range. A source that names an affiliation but not its period leaves the period blank. Sort history chronologically for presentation, with undated entries after dated entries.

## Search and filter experience

Replace the separate search-target picker with one keyword field that searches names, aliases, current and previous affiliations, schools, representative names, and known history seasons. Users can enter a name, organization, or season such as `2022-23`. A season query matches a known single season or any season inside a confirmed range; undated rows do not match a season query. Keep the current all-token matching behavior for multi-word queries.

Move conditions into a clearly labeled filter page opened by a `絞り込み` button. Provide single selections for division, current team, and position, plus an affiliation type selection for club, university, or high school. Conditions from different groups use AND logic. The page shows the selected value for each group and offers `絞り込み条件を初期化` and `この条件で検索` actions. Initializing filters returns only those filter groups to their default values and preserves the keyword.

On the result screen, show the number of matches and a concise summary of the keyword and active filters. The filter button shows how many condition groups are active. A `検索条件をすべて解除` action clears both the keyword and filters. Search result cards show the current team and up to two recent prior affiliations with their type and confirmed season; the full history remains on the player detail page.

Set search-field colors explicitly: navy input text, readable muted placeholder text, white field background, and a visible border. Keep the colors independent of the Android system theme. Use clear labels, adequate text sizes, and touch targets of at least 44 dp for filter controls.

## Data flow and failure handling

Use existing per-player source URLs as the first lookup path. Gather candidate affiliations from the profile, then check official sources for identity and date details. Players without a Rugby Database profile still receive a review using available official sources. A failed, missing, or ambiguous page does not create data. Keep the existing catalog value when a new source cannot confirm a replacement.

The bundled catalog remains readable offline. Search and filters operate only on the validated local catalog and do not depend on network availability.

## Validation

- Extend parser validation for supported affiliation types while accepting older rows without a type as clubs.
- Test history-type and season search, empty or unconfirmed dates, and combinations of keyword, division, team, position, and affiliation-type filters.
- Validate all 1,480 bundled player records, IDs, source URLs, date ranges, and affiliations before packaging.
- Review the complete JSON diff and report how many players gained each affiliation type and how many histories remain unconfirmed.
- Run the local test suite and Android Release build. Inspect the search and filter screens on a device when the device is unlocked and available. Do not use GitHub Actions.

## Options considered

1. Use only official pages. This has the strongest source quality but may leave gaps where historical pages are unavailable.
2. Use Rugby Database to find candidates, then validate with official pages where possible and keep the specific cited source for every fact. This balances coverage and traceability and is selected.
3. Copy every Rugby Database history entry automatically. This is faster but risks treating season appearances as continuous club tenure and is rejected.
