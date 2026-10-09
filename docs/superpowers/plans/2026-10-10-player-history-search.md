# Player History and Search Implementation Plan

> **For agentic workers:** Execute inline in this session using the approved design. Tasks use checkbox (`- [ ]`) syntax.

**Goal:** Enrich all 1,480 bundled player records with sourced club and school affiliations and make player search and filters clear and readable.

**Architecture:** Extend the existing JSON-backed `PlayerTeamHistory` model with a backwards-compatible category. Keep filtering/search local and deterministic. Present filters on a dedicated page and keep the bundled catalog as the only runtime data source.

**Tech Stack:** .NET 9, MAUI, C#, xUnit, bundled JSON.

## Global Constraints

- Review all 1,480 records and retain source URLs, publisher, checked date, and fields.
- Use RugbyDB season entries as the discovery/reference starting point; corroborate current club and explicit tenure dates using official sources when available.
- Never infer an affiliation or period from a gap; unknown periods stay blank.
- Preserve the existing `schools` summary and representative history model.
- Do not add runtime scraping or a remote database.
- Work on `main` only and do not trigger GitHub Actions.

---

### Task 1: Model, parser, and search behavior

**Files:** `PlayerCatalogModels.cs`, `PlayerCatalogParser.cs`, `PlayerSearch.cs`, `tests/OneRugbyNavi2.Core.Tests/PlayerCatalogTests.cs`

- [ ] Add history type with default `club`; allow only `club`, `university`, `highSchool`.
- [ ] Add tests that legacy rows without type parse as club, invalid type is rejected, and season queries match single/ranged seasons but not undated history.
- [ ] Search all history categories and dates while preserving token matching and null-safety.
- [ ] Run `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore -v:minimal`.

### Task 2: Filter and result UX

**Files:** `PlayersPage.cs`, `PlayerDetailPage.cs`, `PageStyles.cs`, `Resources/Styles/Styles.xaml`, `tests/OneRugbyNavi2.Core.Tests/PlayerCatalogTests.cs`

- [ ] Replace scope picker and inline condition pickers with a labeled keyword field and `絞り込み` route to a dedicated condition page.
- [ ] Provide division, current team, position, and affiliation category filters; combine groups with AND; preserve keyword when resetting filters.
- [ ] Show result count and active-condition summary; provide a complete reset action; show up to two recent previous affiliations with category and season.
- [ ] Set explicit SearchBar text, placeholder, background, and border colors and provide 44 dp touch targets.
- [ ] Sort detail history by season, undated entries last, and label each category.
- [ ] Run targeted tests and inspect the complete diff.

### Task 3: Catalog research and enrichment

**Files:** `Resources/Raw/league-one-players-2026-27.json`, `docs/player-catalog.md`

- [ ] Review all player source URLs and RugbyDB season-level team/school records.
- [ ] Record supported club/university/high-school affiliations with stable IDs and source references; retain existing values where new evidence is unavailable.
- [ ] Cross-check current club and explicit tenure intervals against official sources where available, especially Maruyama Rintaro.
- [ ] Validate the full catalog, unique player IDs, source URLs, season ranges, and count of unknown periods; review JSON diff and report coverage.

### Task 4: Review and local validation

**Files:** all changed files

- [ ] Review compatibility, search conditions, sort boundary cases, data provenance, and unrelated catalog behavior; address findings.
- [ ] Run the local .NET test suite and Android Release build using the installed SDK path if needed.
- [ ] If the Android device is available and unlocked, inspect search/filter screens; otherwise record the visual check as unverified.
- [ ] Confirm no GitHub Actions ran and inspect `git diff --check`, full diff, and repository status.
