# Multi-Team Favorites and Quick Schedule Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Support multiple saved favorite teams, a one-tap filter scoped to the selected division, and favorite-aware initial division selection without automatically hiding fixtures.

**Architecture:** Put favorite collection parsing, migration, identity-aware add/remove, and division-priority selection into a small pure C# helper linked into the Core.Tests project. Keep transient favorite-filter state separate from `TeamFilter` in `ScheduleViewModel`, and let `MainPage` own Preferences, UI state transitions, and startup/season selection.

**Tech Stack:** .NET 9, C#, .NET MAUI, MAUI Preferences, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-03-multi-team-favorites-design.md`

## Global Constraints

- Preserve the existing main-only workflow; do not create a branch.
- Persist favorite names as a JSON string collection in MAUI Preferences under the `FavoriteTeams.v2` key.
- When that key is absent or invalid, read the legacy `FavoriteTeam` value, migrate it once, and write a valid v2 collection.
- Favorite-filter mode is transient and is not restored on app launch.
- Do not change schedule acquisition, cache contents, match ordering, or official source data.
- Do not add a new package or a permanent second navigation surface.
- The favorite filter applies only within one division at a time; division priority is D1, then D2, then D3.

## Review Focus

- Invalid or empty v2 preference data with a legacy value must preserve the legacy team and write valid v2 JSON (Task 1: `InvalidV2MigratesLegacyFavorite`).
- Current and historical aliases must not create duplicate favorites, and the official displayed names must remain unchanged (Task 1: `ToggleFavoriteUsesSeasonAwareIdentity`).
- A favorite absent from the loaded schedule must not cause a guessed division or hide the default schedule (Task 1: `FindHighestFavoriteDivisionReturnsNullWhenUnmatched`; Task 3: `NoFavoriteMatchLeavesScheduleVisible`).
- Division changes, refresh, and season changes have different selection behavior and must not accidentally restore transient favorite mode (Task 3: `CategoryChangeClearsFavoriteMode`, `RefreshPreservesCategoryAndFavoriteModeIsNotRestored`, `SeasonChangeSelectsHighestFavoriteDivision`).
- Venue and period filters must survive favorite filter on/off while manual team selection and clear-filters follow their specified exit behavior (Task 2: `FavoriteFilterCombinesWithIndependentFilters`; Task 3: `ManualTeamAndClearFiltersExitFavoriteMode`).

## File Map

- Create `FavoriteTeamLogic.cs`: pure JSON migration, season-aware favorite set operations, favorite-match resolution, and D1/D2/D3 priority selection.
- Modify `ScheduleViewModel.cs`: add a separate favorite-team filter collection and OR-match it with the existing venue and period filters.
- Modify `MainPage.xaml`: add the compact star toggle beside the existing filter summary and show the favorite count and management state in the existing panel.
- Modify `MainPage.xaml.cs`: bridge MAUI Preferences to the helper and implement startup/season selection, favorite management, toggle behavior, and exit transitions.
- Modify `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj`: link `FavoriteTeamLogic.cs` into the pure core test assembly.
- Create `tests/OneRugbyNavi2.Core.Tests/FavoriteTeamLogicTests.cs`: cover migration, season-aware deduplication, favorite matching, and division priority.

### Task 1: Favorite Collection and Season-Aware Selection Logic

**Files:**
- Create: `FavoriteTeamLogic.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj`
- Create: `tests/OneRugbyNavi2.Core.Tests/FavoriteTeamLogicTests.cs`

**Interfaces:**
- Produces `FavoriteTeamLogic.Load(string? json, string? legacyTeam) -> FavoriteLoadResult`, where the result contains an ordered, deduplicated `IReadOnlyList<string>` and whether migration/writeback is required.
- Produces `FavoriteTeamLogic.Toggle(IReadOnlyList<string> favorites, string team, int seasonStartYear) -> IReadOnlyList<string>`; identity comparison uses `SeasonCatalog.AreSameTeamName` with the supplied season year.
- Produces `FavoriteTeamLogic.FindHighestFavoriteDivision(IReadOnlyList<MatchItem> div1, IReadOnlyList<MatchItem> div2, IReadOnlyList<MatchItem> div3, IReadOnlyList<string> favorites) -> int?`, returning 1, 2, 3, or null.
- Produces `FavoriteTeamLogic.MatchAnyFavorite(MatchItem match, IReadOnlyCollection<string> favorites) -> bool`, comparing each favorite against both sides using the match season year.

- [ ] **Step 1: Write failing tests** for `InvalidV2MigratesLegacyFavorite`, `ValidV2DeduplicatesNames`, `ToggleFavoriteUsesSeasonAwareIdentity`, `FavoriteMatchUsesHistoricalAlias`, `FindHighestFavoriteDivisionPrefersD1ThenD2ThenD3`, and `FindHighestFavoriteDivisionReturnsNullWhenUnmatched`. Assert invalid V2 plus `"Legacy Club"` returns that one favorite with migration required; duplicate identities collapse; toggling a rebranded alias removes the existing identity; a historical alias matches; D1 wins over D2/D3, otherwise D2 wins; unmatched returns null.
- [ ] **Step 2: Run the focused tests and confirm failure** with `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore --filter FullyQualifiedName~FavoriteTeamLogicTests`; expected: compile/test failure because `FavoriteTeamLogic` and its result type do not exist.
- [ ] **Step 3: Implement the four exact helper interfaces above** in `FavoriteTeamLogic.cs`; malformed/empty JSON falls back to legacy, valid JSON is a string array, names remain raw display names, and aliases only affect identity comparisons.
- [ ] **Step 4: Link the helper into Core.Tests and rerun the focused tests**; expected: all six tests pass.
- [ ] **Step 5: Run the complete Core.Tests project** with `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore`; expected: all tests pass.
- [ ] **Step 6: Commit** as `feat: add multi-team favorite logic`.

### Task 2: Division-Scoped OR Filtering in the View Model

**Files:**
- Modify: `ScheduleViewModel.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/FavoriteTeamLogicTests.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj`

**Interfaces:**
- Consumes Task 1's `FavoriteTeamLogic.MatchAnyFavorite(MatchItem, IReadOnlyCollection<string>)`.
- Produces `ScheduleViewModel.FavoriteTeamFilters`, an optional `IReadOnlyCollection<string>` state separate from `TeamFilter`; empty means inactive.
- `ApplyFilters()` applies regular `TeamFilter` when set, otherwise favorite OR matching when favorite filters are set, then applies venue and period filters exactly as before.

- [ ] **Step 1: Link `ScheduleViewModel.cs` and its production dependencies into Core.Tests**, adding only the compile links required by compiler diagnostics and no packages.
- [ ] **Step 2: Add `FavoriteFilterCombinesWithIndependentFilters` coverage** for two favorites in the active division, an unrelated team, one matching venue, and the configured period; assert only a match involving either favorite that also passes venue and period remains. Add a check that empty favorites preserve all matches subject to independent filters.
- [ ] **Step 3: Run the focused test** with `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore --filter FullyQualifiedName~FavoriteFilterCombinesWithIndependentFilters`; expected: compile/test failure because the ViewModel does not yet expose or apply `FavoriteTeamFilters`.
- [ ] **Step 4: Implement `FavoriteTeamFilters` and the OR branch in `ScheduleViewModel.ApplyFilters()`**, ensuring existing manual `TeamFilter` keeps its single-team meaning and independent filters remain ANDed.
- [ ] **Step 5: Run the focused test then the complete Core.Tests project**; expected: focused test passes and the full suite passes.
- [ ] **Step 6: Commit** as `feat: filter schedule by favorite teams`.

### Task 3: Preferences, Compact Toggle, and UI State Transitions

**Files:**
- Modify: `MainPage.xaml`
- Modify: `MainPage.xaml.cs`
- Modify: `FavoriteTeamLogic.cs` and its tests with pure category-selection helpers needed to test startup/season precedence.
- Modify: `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj` if required to link the exact production type used by those pure helpers.

**Interfaces:**
- Consumes Tasks 1 and 2 helper/view-model interfaces.
- `MainPage` reads `FavoriteTeams.v2`, calls `FavoriteTeamLogic.Load`, and writes the returned collection as JSON; write legacy migration once.
- `MainPage` sets `ScheduleViewModel.FavoriteTeamFilters` only while quick-favorite mode is active and the selected category is D1, D2, or D3.

- [ ] **Step 1: Add pure tests** for `SeasonChangeSelectsHighestFavoriteDivision` and `NoFavoriteMatchLeavesScheduleVisible`, asserting no favorite match yields null/default handling rather than a guessed division.
- [ ] **Step 2: Run the focused tests and confirm failure**; expected: the new selection helper/test seam is not implemented.
- [ ] **Step 3: Implement multi-favorite Preferences migration and add/remove management**: update the existing favorite action based on the selected team, preserve all other favorites, clear `TeamFilter` and picker selection after add/remove, exit favorite mode, and retain venue/period values. Update the expanded panel button for add/remove state and the compact summary count.
- [ ] **Step 4: Implement the compact star toggle and transitions**: disabled/unregistered with no favorites; toggling on filters favorites present in the current division, or switches to the highest matching division; toggling off clears only favorite filters; team picker, division tabs, and clear-filters exit favorite mode as specified. If none match the selected season, leave the schedule unchanged and show a brief status message.
- [ ] **Step 5: Implement startup and season choice**: after first-load data arrives choose highest favorite division without enabling favorite mode; ordinary refresh preserves current division; season change chooses the highest matching favorite division, otherwise keeps a valid current division/default. Preserve venue/period filters.
- [ ] **Step 6: Run Core.Tests** with `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore`; expected: all tests pass.
- [ ] **Step 7: Build Android Release** with `dotnet build OneRugbyNavi2.csproj -f net9.0-android -c Release --no-restore`; expected: build succeeds and emits the Release Android package outputs.
- [ ] **Step 8: Review the full diff against the spec**, verify no branch was created and no schedule acquisition/cache/order/source code changed, then commit implementation and plan as `feat: add multi-team favorite quick filter`.

