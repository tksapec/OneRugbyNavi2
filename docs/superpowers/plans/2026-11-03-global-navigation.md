# App-wide bottom navigation implementation plan

## Scope
Review every in-app `ContentPage` and make the schedule, teams, players, and more navigation available consistently across main, list, detail, information, ranking, and player-filter screens. Preserve existing page content, back/close behavior, and main-screen update/cache actions.

## Implementation
- Add a shared navigation builder in `PageStyles` with selected-section styling and shared Shell destinations.
- Keep MainPage's existing extra update/cache actions in its More menu; show the common navigation menu from More on other pages.
- Wrap TeamListPage, PlayersPage, RankingPage, InfoPage, MatchDetailPage, PlayerDetailPage, and PlayerFiltersPage with the shared fixed footer.
- In modal pages, dismiss the modal before switching tabs or opening the common menu.
- Add route mapping tests for all four sections.

## Verification
- Core tests: 177 passed.
- Android Debug preview build: succeeded, 0 errors; two existing warnings (CA1422 and XC0022).
- Motorola Edge 40: verified bottom navigation and tab switching on schedule, teams, players, info, ranking, match detail, player detail, and player filters. Verified team and player transitions from lists/details, More menu from main/team/detail/modal, and modal dismissal before menu display.
- `git diff --check`: passed with existing line-ending notices.
- Device stay-awake setting restored after the run.

## Review
Independent review found no significant defects. The reviewer flagged inconsistent More behavior; it was unified to open the common menu while preserving MainPage's existing update/cache actions, then verified on device from a team page, a pushed match detail, and a modal player filter.

## Constraints
All changes remain uncommitted on the main working tree. No GitHub Actions or push was run. Existing dirty/unrelated changes were retained.
