# Schedule polish implementation plan

> Main-only implementation. No GitHub Actions, commit, or push.

## Goal
Match the supplied schedule illustration on the connected Android device while preserving schedule, favorites, result visibility, and existing navigation.

## Implemented
- Compact header with season, refresh, and menu actions.
- All/favorites scope and D1-D3 division tabs.
- Compact filter row with funnel icon, active-filter count, quick clear, and next-match action.
- Date-grouped fixtures with full team names, team logos, paired scores, venue, and detail action.
- Fixed four-item bottom navigation with calendar, teams, jersey, and more icons.
- Compact cache/status notification so fixture rows have more visible height. Detailed update status remains available from the menu.
- Preserve schedule cache/results and favorites behavior; next-match scroll keeps its date-group header visible.

## Verification
- Core test suite: 173 passed, 0 failed.
- Android Debug preview build: succeeded, 0 errors. Existing warnings: CA1422 for deprecated Android status bar API and XC0022 for a binding that could be compiled.
- `git diff --check`: passed; Git reports line-ending notices for MainPage.xaml and ScheduleCacheStore.cs.
- Installed the latest 66,904,531 byte preview APK at 2026-11-03 15:38 on Motorola Edge 40 (ZY22H7V26L), launched it, and captured `artifacts/illustration-ui-preview-2026-11-03/device-final.png`.
- Screenshot/UI hierarchy confirm the compact header, all/favorites controls, D1-D3, date group, two-row team presentation, and four fixed bottom items. Tapping the Team item opened TeamListPage and its team catalog.
- Tapping the next-match action showed active-filter count `1` and `解除`; tapping `解除` cleared the upcoming filter. Opening the filter panel and closing it both worked.
- The device had no network connection and showed cached fixture data; live fetch behavior was not validated in this pass.
- Device screen stay-awake setting was restored to false after verification.

## Review
Manual review checked illustration alignment, event-handler references after removing inline count/timestamp display, active-filter counting, filter clearing, grouped scrolling, and bottom navigation. No independent review tool was available in this turn. Existing dirty schedule-fetch/cache changes were retained; no unrelated files were cleaned or overwritten.

## Not verified
- TalkBack reading order, enlarged text/narrow-screen layouts, Release runtime, and live network refresh.
- Match analysis data availability and the next-phase statistics, match timeline, and player list.
