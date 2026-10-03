# Multi-Team Favorites and Quick Schedule Filter Design

## Goal

Let users save more than one favorite team without hiding other teams' fixtures automatically, then apply a one-tap favorite filter when they want it. On first app load, open the highest division that contains a favorite while keeping that division's full schedule visible.

## User intent and constraints

- Registering a team as a favorite must not, by itself, leave the schedule filtered to that team.
- A single quick control should toggle a filter for all registered favorites in the selected division.
- Favorites in higher divisions take precedence when selecting the initial division: D1, then D2, then D3.
- The schedule remains split into D1, D2, and D3 views; the favorite filter does not combine matches from different divisions.
- Preserve venue and period filters when changing the favorite filter.
- Preserve the existing main-only workflow; do not create a branch.

## Approved behavior

### Favorite registration

- Store a set of favorite team names rather than one name.
- In the expanded filter panel, the existing favorite action adds the currently selected team or removes it when it is already saved.
- After adding or removing a favorite, clear the team-only schedule filter and picker selection so the full division schedule returns. Venue and period selections remain in effect.
- Adding or removing a favorite exits quick-favorite-filter mode before returning to the normal schedule.
- Show the saved favorite count in the compact filter summary. In the expanded filter panel, selecting a team shows whether it is already a favorite and the existing action button adds or removes that team. Keep this management in the existing panel to avoid adding permanent vertical UI.
- Migrate the existing `FavoriteTeam` preference into the new favorite collection on first read. The migration preserves the existing team.

### Initial division selection

- After the selected season's schedule data is loaded on the initial app visit, choose the highest-priority division containing a saved favorite: D1, then D2, then D3.
- Do not enable the favorite filter during startup. Show every fixture in the chosen division, subject only to independently retained venue and period filters.
- If no saved favorite can be matched to the loaded season, keep the existing default division behavior.
- A normal refresh preserves the user's current division. When the user changes season, choose the highest-priority division containing a favorite in that season; if none can be matched, retain the current division when available, otherwise use the existing default.

### Quick favorite filter

- Add one compact star button near the existing filter summary. Disable it and show an unregistered state when there are no saved favorites.
- When pressed, filter the selected division to fixtures involving any saved favorite that appears in that division. Favorite matching uses the season-aware team identity rules already used by the app.
- If the selected division has no matching favorite but another division does, switch to the highest-priority division that contains a favorite, then apply the filter there.
- Pressing the button again clears only the favorite team filter. Venue and period filters remain active.
- Selecting a team manually in the team picker exits favorite-filter mode and applies the regular single-team filter.
- Selecting a D1/D2/D3 tab exits favorite-filter mode and shows that division's normal schedule, while preserving venue and period filters. If a regular manual team filter cannot exist in the newly selected division, clear that team filter as the existing flow does.
- Clearing filters exits favorite-filter mode and clears the team, venue, and period filters as it does today.
- If saved favorites cannot be matched to any fixture in the selected season, tapping the button leaves the list unchanged and gives a brief, non-blocking status message.

## Data and state

- Persist favorite names as a JSON string collection in MAUI Preferences under the `FavoriteTeams.v2` key. When that key is absent or invalid, read the legacy `FavoriteTeam` value, migrate it once, and write a valid v2 collection. Use season-aware aliases to avoid duplicate identities when the user adds the same club under a current or historical display name.
- Keep ordinary `TeamFilter` behavior for the team picker. Represent favorite-filter mode separately as a set of team names so it can match any favorite in the current division without changing the meaning of the existing picker.
- Favorite-filter mode is transient and is not restored on app launch. This guarantees startup shows the complete schedule.
- Do not change schedule acquisition, cache contents, match ordering, or official source data.

## UI and implementation scope

- Update `MainPage.xaml` for the compact quick-filter button and multi-favorite summary/management affordance.
- Update `MainPage.xaml.cs` for add/remove behavior, legacy preference migration, startup division choice, quick-filter toggling, and transitions between favorite and regular filters.
- Update `ScheduleViewModel.cs` only as needed to apply an OR match over favorite teams while preserving the existing single-team filter behavior.
- Extend `tests/OneRugbyNavi2.Core.Tests` for favorite set filtering, alias matching, division priority, and preservation/clearing of independent filters.
- Do not add a new package or a permanent second navigation surface.

## Acceptance criteria

1. A legacy single favorite is available after migration and can be removed.
2. Adding another favorite preserves both favorites, avoids duplicate identities, and restores the unfiltered team list immediately.
3. On first load, a D1 favorite takes priority over D2 and D3 favorites; without a D1 favorite, D2 takes priority over D3. The chosen division is not favorite-filtered on startup.
4. The quick control filters to all matching favorite teams in the active division, combines with venue and period filters, and clears only the favorite filter when toggled off.
5. Pressing the quick control while on a division with no favorite switches to the highest division that has matching favorite fixtures and filters that division.
6. Manual team selection, division selection, and clear-filters actions exit favorite-filter mode according to the behaviors above.
7. No favorites or no schedule matches leave the app in a usable, unfiltered state and never hide unrelated matches merely because favorites are saved.
8. Core tests pass and an Android Release build succeeds.

## Risks and limits

- Team identity aliases are season-sensitive. Tests must cover the existing 2026 rebrand aliases and verify that raw official team names remain unchanged.
- Division choice depends on team membership in the successfully loaded schedule data. An unpublished or missing schedule row may prevent a favorite from being resolved; in that case use the documented fallback rather than guessing its division.
- The quick filter applies only within one division at a time. Showing favorites from multiple divisions simultaneously is outside this design.
