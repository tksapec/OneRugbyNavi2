# Div1 Player Catalog Audit Plan

> **For agentic workers:** Execute this plan task by task. Do not start GitHub Actions, publish, or merge.

**Goal:** Reconcile the complete 2026-27 Div1 player catalog against current official team and League One sources, one team at a time.

**Architecture:** This is a source-led data audit, not an application code change. Work in the existing catalog JSON using its player IDs, provenance schema, and history rules. Complete all checks for one team before proceeding to the next, and preserve all unrelated working-tree changes.

**Tech Stack:** JSON, Python catalog validator and normalization tools, .NET 9 / xUnit for relevant catalog regressions.

**Spec:** `docs/superpowers/specs/2026-11-15-div1-player-catalog-audit-design.md`; data rules in `docs/player-catalog.md`.

## Global Constraints

- Target season: `2026-27`; audit only teams marked `DIV1` in `SeasonCatalog.cs`.
- Current baseline: 12 teams and 667 player records; re-count before editing.
- Change only facts directly supported by official sources; record publisher, exact URL, checked date, and fields.
- Use independent sources only to corroborate identity or history, not to silently override conflicting primary facts.
- Do not infer transfer dates, categories, positions, nationality, or identities from unrelated facts.
- Preserve Div2/Div3 data and all pre-existing user changes.
- Do not change application code, portraits, manifest, remote repository, or trigger GitHub Actions.

## Review Focus

- Team roster omissions, stale current-team assignments, and players no longer listed: compare the full official roster against all records for that team and assess each unmatched row before changing it.
- Identity collisions or same-name players: verify official player IDs/profile URLs and available player-specific facts before adding or merging.
- Profile fields that differ by source or season: retain only dated, player-specific evidence and report unresolved conflicts.
- History dates inferred from current rosters: change only with player-specific season tables or transfer announcements.
- Source provenance gaps: ensure every changed field is named in the corresponding source's `fields` array and the check date is `2026-11-15`.

## Files

- Modify: `Resources/Raw/league-one-players-2026-27.json` — supported Div1 roster and profile corrections only.
- Read: `SeasonCatalog.cs` — authoritative current team list and team IDs.
- Read: `docs/player-catalog.md` — catalog facts and history policy.
- Run: `tools/validate_player_catalog.py`, `tools/deduplicate_player_catalog.py`, `tools/normalize_player_history.py`, and relevant catalog tests as appropriate.

## Team Sequence

Complete each team fully before advancing, using the order in `SeasonCatalog.cs`:

1. 浦安D-Rocks
2. クボタスピアーズ船橋・東京ベイ
3. コベルコ神戸スティーラーズ
4. 埼玉パナソニックワイルドナイツ
5. 静岡ブルーレヴズ
6. 東京サントリーサンゴリアス
7. 東芝ブレイブルーパス武蔵
8. 栃木ホンダヒート
9. トヨタヴェルブリッツ
10. 三菱重工相模原ダイナボアーズ
11. 横浜キヤノンイーグルス
12. リコーブラックラムズ東京

For each team:

- [x] Find the current club roster and League One team/player pages using `agent-reach` web search/reading; retain the exact source URLs.
- [x] Compare the official roster to every catalog record for the team; verify additions, departures, names, and identity with player profile IDs.
- [x] Review each available primary-source player field: positions, birth date, height, weight, schools, and 2026-27 registration category. Update only when explicitly supported.
- [x] Check candidate history corrections against player-specific seasons or transfer announcements; keep uncertainty unknown.
- [x] Add or update source provenance only for reviewed facts. Review duplicates for the team before marking it complete.
- [x] Record team URLs, added/removed/corrected player IDs and fields, and unresolved conflicts in [the audit report](../../reports/2026-11-15-div1-player-catalog-audit.md).

## Final Verification

- [x] Re-parse JSON, count records/teams, and run `python tools/validate_player_catalog.py`; require zero validation errors.
- [x] Run deduplication and normalization in preview first; apply only if the diff is supported and confined to reviewed data.
- [x] Run relevant catalog tests and the Core test suite if the data diff or project setup makes them useful; record any failures separately from audit findings.
- [x] Review the catalog diff for unsupported facts, unrelated changes, Div2/Div3 changes, or broad formatting churn.
- [x] Report team coverage, affected records and fields, unresolved conflicts, validation/test results, and that no Actions or publishing occurred.
