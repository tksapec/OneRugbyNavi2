# UI and Usability Improvements Design

## Intent
Improve the six previously identified OneRugbyNavi2 UI areas while preserving the app's current MAUI navigation and rugby schedule workflows.

## Goals
- Make match-detail navigation obvious and easy to tap without changing the separate team-logo official-page actions.
- Keep failures and cached-data status visible until users dismiss or retry; keep success notices brief.
- Make favorite filtering look and behave like a button, with a clear active state and accessible label.
- Keep expanded filters and the menu usable on short screens through bounded scrolling.
- Reduce the player search page's fixed header height by grouping catalog status and update action.
- Use one navigation menu presentation on every page and keep menu typography aligned with shared button styles.
- Keep shared palette roles consistent across XAML and code-created screens, reserving warning/success colors for status meaning.

## Non-goals
No new packages, navigation model, data behavior, or schedule source changes. No speculative redesign of colors or branding.

## Approaches
1. Replace the navigation design wholesale — rejected because it would expand risk and disrupt established flows.
2. Refine the existing MAUI views in place — selected because it preserves current workflows and limits changes to the requested UI.

## Implementation choices
Use a full-card detail tap target with explicit semantic description while keeping logo buttons independent. Keep transient success messages auto-hiding; make failures, cache fallback, and consistency warnings persist with an explicit dismiss/retry affordance. Convert the favorite quick-filter label into a button. Bound expanded filter content with scrolling. Use the shared native action sheet for navigation on every page, with schedule-specific actions added to its item list. Combine player catalog status and update progress/action into one compact row above results. Preserve the existing semantic palette values and use the shared menu button style.

## Constraints
Preserve main-only workflow, existing dirty work, and current data semantics. Verify with relevant existing tests and Android build; real device layout/accessibility remains a separate check.
