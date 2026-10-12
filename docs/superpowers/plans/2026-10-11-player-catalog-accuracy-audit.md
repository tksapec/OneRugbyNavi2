# Player Catalog Accuracy Audit Plan

> **For agentic workers:** Use `superpowers:executing-plans` to execute this plan task-by-task. Do not start GitHub Actions.

**Goal:** Raise the factual accuracy of the current League One player catalog by reconciling player identity, current roster facts, and sourced history against multiple reliable public sources.

**Architecture:** Audit the existing catalog in repeatable batches. First run structural and duplicate checks, then compare suspicious or high-impact facts with current official club and League One profiles, J SPORTS, Rugby Database, and Wikipedia where useful. Change only facts with direct evidence; keep unknowns empty and record source publisher, URL, checked date, and fields.

**Tech Stack:** JSON, Python audit scripts, .NET 9, xUnit.

**Spec:** `docs/player-catalog.md` and `docs/superpowers/specs/2026-10-11-player-catalog-update-design.md`.

## Global Constraints

- Base the work on the fetched `origin/main` commit `2d58c0d` and do not overwrite updates that arrived there.
- Do not launch GitHub Actions, publish, deploy, or merge to `main` during this audit.
- Do not add or restore player portraits or photo display.
- Do not infer nationality, eligibility category, position, or club tenure from unrelated facts.
- Preserve valid identities, IDs, sources, and existing user-facing search behavior.
- Record only facts supported by sources; leave uncertain fields blank and report unresolved conflicts.

## Review Focus

- Duplicate or misattributed identities introduced by data refresh: compare official IDs, normalized names, schools, DOB, team, and source content; add regression checks.
- Club and position label scraping artifacts: verify player-level official cards rather than nearby text labels or roster badges.
- Contradictory body measurements and names: prefer current primary sources, preserve source dates, and do not merge on matching bio alone.
- Historical team seasons: require player-specific seasons or transfer announcements; do not bridge unverified gaps.
- Provenance integrity: each non-empty added/corrected field must have a source entry that actually supports it.

## Task 1: Baseline and structural audit

**Files:** inspect `Resources/Raw/league-one-players-2026-27.json`, `PlayerCatalogDuplicateDetector.cs`, `tools/deduplicate_player_catalog.py`, and the catalog tests.

- [ ] Capture catalog counts, source counts, and current test baseline.
- [ ] Validate schema, IDs, team IDs/names/divisions, source URLs/fields/dates, seasons, and registration categories.
- [ ] Find exact normalized-name collisions, official-ID collisions, multi-fact bio collisions, and questionable identity merges.
- [ ] Review the latest refresh and deduplication code to identify deterministic data-loss or misattribution risks.

## Task 2: Source-led fact review

**Files:** update only the bundled player JSON and focused catalog tests.

- [ ] Compare each team roster against the saved current-season list; prioritize positions, Japanese/English names, newcomers, and roster changes.
- [ ] Check suspicious profile facts against League One and club profiles; use J SPORTS and Rugby Database as independent corroboration and Wikipedia for biographies/nationality only where explicitly stated.
- [ ] For each confirmed correction, update the exact source list and checked date, and preserve verified alternate spellings as aliases where appropriate.
- [ ] Keep disagreements unresolved unless dated, player-specific evidence establishes which value applies; report those conflicts.

## Task 3: Verification and review

- [ ] Re-parse the complete JSON and re-run duplicate, team consistency, provenance, and history-form checks.
- [ ] Add/adjust regression tests for each confirmed error pattern; run the complete Core test suite.
- [ ] Review the complete diff for accidental bulk edits, photo changes, unsupported values, or unrelated product changes.
- [ ] Report corrected records, source coverage, unresolved conflicts, test results, and that no Actions or main merge was performed.
