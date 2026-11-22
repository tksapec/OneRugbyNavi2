# Div1 Player Catalog Audit Design

## Intent

Update the 2026-27 League One Div1 players in the bundled player catalog by checking each of the 12 current teams one at a time. The goal is a current, accurate roster and player profile data with traceable sources.

## Goals

- Reconcile the 667 existing Div1 records against each club's current official roster and League One player profiles.
- Correct confirmed names, current clubs, positions, birth dates, physical measurements, schools, and season-specific registration categories.
- Add or correct player records only when identity is supported by official IDs, profile links, names, and available facts.
- Check historical club rows against player-specific season records or transfer announcements; leave uncertain periods unchanged or unknown.
- Record the source publisher, exact URL, checked date, and supported fields on affected records.
- Preserve Div2/Div3 catalog records and all unrelated workspace changes.

## Non-goals

- Updating portraits, schedule data, app behavior, or the player catalog update service.
- Publishing, committing, merging, deploying, or triggering GitHub Actions.
- Guessing missing biographical facts, transfers, or eligibility categories.

## Approaches

1. Use the League One site as the sole source for all facts. This is consistent and efficient, but does not independently resolve profile gaps or historical ambiguities.
2. Use club official rosters as the current-season roster source, League One player profiles for player facts, and independent season records or official transfer notices only to corroborate identity and history. This requires more checks but best fits the catalog's provenance policy and the request for a careful team-by-team review.

Chosen approach: 2. Club and League One primary sources determine current facts; secondary sources are corroboration only and cannot override a conflicting primary source without dated, player-specific evidence.

## Constraints and data rules

- Target season is 2026-27, matching the current bundled catalog.
- Audit only the 12 teams labeled `DIV1` in `SeasonCatalog.cs`.
- A current team roster alone does not establish the start date of a historic stint.
- Do not infer nationality, registration category, position, or transfer tenure from unrelated fields.
- Use the existing player identity and schema rules in `docs/player-catalog.md`.
- Preserve all pre-existing user changes and do not edit unrelated application code.

## Implementation choices

- Process teams in the order listed by `SeasonCatalog.cs`, completing roster comparison, player-level fact checks, provenance updates, and duplicate review for a team before moving to the next.
- Modify `Resources/Raw/league-one-players-2026-27.json` only for supported corrections and additions; do not change the manifest.
- Use the existing catalog validator, deduplicator, normalizer, and focused tests as appropriate. Run the catalog validator and relevant test/build checks after the complete catalog review.
- Keep a concise audit record of checked team source URLs, confirmed changes, and unresolved source conflicts in the task report; do not add speculative data to the catalog.

## Completion criteria

- All 12 Div1 rosters have been checked against current official sources.
- Every changed or added fact has a source entry whose `fields` cover that fact and whose `checkedOn` date reflects this audit.
- Catalog validation succeeds, and the final diff contains only supported catalog changes plus this audit design/plan documentation.
- The report lists team coverage, confirmed record changes, unresolved conflicts, checks performed, and states that no publishing or Actions run occurred.
