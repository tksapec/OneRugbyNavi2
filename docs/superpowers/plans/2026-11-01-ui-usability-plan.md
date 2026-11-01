# UI and Usability Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Improve six identified UI areas across schedule, menu, and player search screens.

**Architecture:** Refine existing MAUI XAML and code-behind without changing routes or data contracts. Keep transient and persistent message behavior explicit, and retain independent logo actions.

**Tech Stack:** .NET MAUI, C#, XAML

**Spec:** docs/superpowers/specs/2026-11-01-ui-usability-design.md

## Global Constraints
- Stay on main; do not create branches.
- Preserve unrelated or dirty work.
- Do not trigger GitHub Actions.
- Avoid new dependencies and keep official team links separate from match details.

## Review Focus
- Match card tap targets do not intercept logo button actions.
- Persistent notices can be dismissed and do not cover schedule controls.
- Favorite quick filter reflects active and unavailable states accessibly.
- Scroll containers remain bounded by the available screen height.
- Player search header remains usable at narrow widths and large text settings.

### Task 1: Schedule detail and status UX
**Files:** Modify `MainPage.xaml`, `MainPage.xaml.cs`.
- [x] Make match card surface open details; remove fragile chevron-only recognizer and add accessible hint.
- [x] Keep logo buttons independent and add tap guards if bubbling is possible.
- [x] Make failure/cache/consistency messages persistent and dismissible; preserve auto-hide for success.
- [x] Convert favorite quick-filter label to a button, update active text/state/accessibility.
- [x] Run core tests and inspect diff.

### Task 2: Compact screen scrolling
**Files:** Modify `MainPage.xaml`.
- [x] Place expanded filter content in a height-bounded scrollable region, with clear/close actions outside the scroller.
- [x] Place menu items in a scrollable region while keeping close action reachable.
- [x] Check XAML structure and Android Debug build.

### Task 3: Player search header
**Files:** Modify `PlayersPage.cs`.
- [x] Combine catalog state, update activity, and update action in one compact row.
- [x] Keep result count visible and preserve existing filter/search behavior.
- [x] Check narrow-width layout by reviewing grid sizing and run Android Debug build.

### Task 4: Shared color and navigation menu
**Files:** Modify `PageStyles.cs`, `MainPage.xaml`, `MainPage.xaml.cs`.
- [x] Route all page menu buttons through one shared native action sheet.
- [x] Keep schedule status/cache actions and add rankings to the common navigation choices.
- [x] Match the schedule header menu typography to `PageStyles.SecondaryButton`.
- [x] Confirm palette values in `PageStyles.cs` match the shared XAML palette.

### Task 5: Final review
- [x] Inspect full diff and verify all six requested areas against the design.
- [x] Run `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore -v:minimal` (159 passed).
- [x] Run Android Debug build with configured SDK (succeeded; one pre-existing CA1422 warning) and report warnings and any device-only checks.
