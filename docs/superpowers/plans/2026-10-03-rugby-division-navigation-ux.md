# Rugby Division Navigation UX Implementation Plan

> **For agentic workers:** Execute inline as explicitly requested by the user. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Fix favorite-filter summaries, limit favorites to one club per division, add page-turn motion to D1-D3 swipes, and make the grouped team list cards open official pages.

**Architecture:** Keep favorite collection persistence in `FavoriteTeamLogic` and use loaded season fixtures to identify favorites already assigned to a division. Animate the outgoing schedule capture with a directional page-peel drawable during swipe-driven category changes, then reuse the existing category transition. Keep the team catalog source and URL validation unchanged while grouping its cards by division.

**Tech Stack:** .NET MAUI 9, C#, XAML, Android ADB.

## Global Constraints

- Keep the current main checkout and preserve unrelated work.
- Keep favorites in the existing `FavoriteTeams.v2` preference.
- Use the selected season's fixture names and the existing season-aware identity comparisons.
- Limit swipe navigation to D1, D2, and D3; retain existing tab behavior for other categories.
- Continue opening official team URLs only after `TeamOfficialPageLink.TryCreateUri` validates them.

---

### Task 1: Per-Division Favorite Slots and Summary

**Files:**
- Modify: `FavoriteTeamLogic.cs`
- Modify: `MainPage.xaml.cs`
- Modify: `tests/OneRugbyNavi2.Core.Tests/FavoriteTeamLogicTests.cs`

**Interfaces:**
- Add `FavoriteTeamLogic.ToggleInDivision(IReadOnlyList<string> favorites, string team, int division, int seasonStartYear, IReadOnlyList<MatchItem> div1, IReadOnlyList<MatchItem> div2, IReadOnlyList<MatchItem> div3) -> IReadOnlyList<string>`.
- On add, remove existing favorite identities found in the selected division before appending the selected team. On remove, clear that division's occupied slot.
- During quick filtering, render the resolved current-season favorite team names in the summary instead of a count.

- [x] Add a core test that toggling a new D1 team replaces an existing D1 favorite while preserving D2 and D3 favorites.
- [x] Add a core test that toggling an existing D1 favorite clears the D1 slot.
- [x] Run the focused `FavoriteTeamLogicTests` filter and confirm the new tests fail before implementation.
- [x] Implement the division-aware toggle using existing `MatchAnyFavorite` and season-aware team name comparison helpers.
- [x] Normalize saved favorites against the loaded season so existing duplicate division registrations collapse to one team per DIV.
- [x] Update `OnFavoriteClicked` to call the division-aware toggle and update the summary to list the resolved favorite display names.
- [x] Rerun the focused tests and the full Core.Tests project.

### Task 2: Swipe Page-Turn Animation

**Files:**
- Modify: `MainPage.xaml`
- Modify: `MainPage.xaml.cs`

**Interfaces:**
- Name the schedule-content grid `schedulePageContent`.
- Change `OnScheduleSwiped` to animate toward the next or previous division and then call the existing `SelectCategory` transition.

- [x] Apply the left/right recognizers to the list and match-card surfaces.
- [x] Add a guarded async page-peel animation using a captured schedule page and a directional `GraphicsView` overlay, restoring the overlay state in `finally`.
- [x] Keep the animation from leaving D1-D3 and ignore swipes while a flip is running.
- [x] Build and confirm the XAML animation APIs compile for Android.
- [ ] Confirm the directional page-peel gesture on the physical device.

### Task 3: Division Grouping and Full-Card Team Navigation

**Files:**
- Modify: `TeamListPage.cs`

**Interfaces:**
- Bind the CollectionView as grouped data with ordered DIV1, DIV2, and DIV3 headers.
- Bind one tap recognizer to each complete team card and route it through the existing official URL validator.

- [x] Group the fetched TeamCards by `DivisionCode` in DIV1/DIV2/DIV3 order.
- [x] Render a division header once per group and remove the now-redundant per-card division label.
- [x] Bind a tap gesture to the whole card.
- [x] Build, install, and launch the Android Debug APK on the connected device.
- [ ] On device, verify favorite summary names, one-slot replacement logic, a D2-to-D3 swipe with page-turn motion, group headers, and card navigation to an individual official team URL.
