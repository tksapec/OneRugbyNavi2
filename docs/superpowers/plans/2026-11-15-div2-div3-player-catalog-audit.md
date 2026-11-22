# Div2 and Div3 Player Catalog Audit Plan

> **For agentic workers:** Execute this plan team by team. Do not start GitHub Actions, publish, commit, or merge.

**Goal:** Reconcile the 2026-27 Div2 and Div3 player catalog against current official team and League One sources, one team at a time.

**Architecture:** This is a source-led data audit, not an application code change. Update only supported facts in the existing catalog JSON using its IDs, provenance schema, and history rules. Finish roster, profile, provenance, and duplicate checks for each team before proceeding.

**Tech Stack:** JSON, Python catalog validator/deduplicator/normalizer, Python unittest, .NET 9 / xUnit.

**Spec:** `docs/superpowers/specs/2026-11-15-div2-div3-player-catalog-audit-design.md`; rules: `docs/player-catalog.md`.

## Global Constraints

- Target season: `2026-27`; audit only teams marked `DIV2` or `DIV3` in `SeasonCatalog.cs`.
- Baseline: 15 teams, 431 Div2 records, 358 Div3 records; re-count before editing.
- Change facts only with direct official-source support; record publisher, exact URL, checked date, and supported fields.
- Use independent sources only to corroborate identity or history; report conflicting primary facts.
- Do not infer transfers, categories, positions, nationality, or identity from unrelated facts.
- Preserve Div1 data and all pre-existing user changes.
- Do not change application code, portraits, manifest, remote repository, or trigger GitHub Actions.

## Review Focus

- Compare each full official roster to every catalog record for the team; assess all unmatched records before changing them.
- Verify additions, departures, and potential duplicate identities using player-specific IDs/profile URLs.
- Check positions, birth dates, measurements, schools, and season-specific registration categories against available primary sources.
- Change history only with player-specific season rows or transfer announcements; leave uncertain values unchanged or unknown.
- Confirm source `fields` cover every changed value and `checkedOn` is `2026-11-15`.

## Files

- Modify: `Resources/Raw/league-one-players-2026-27.json` — supported Div2 and Div3 player facts only.
- Read: `SeasonCatalog.cs`, `docs/player-catalog.md`.
- Run: `tools/validate_player_catalog.py`, `tools/deduplicate_player_catalog.py`, `tools/normalize_player_history.py`, focused Python tests, and Core tests as useful.
- Create: `docs/superpowers/reports/2026-11-15-div2-div3-player-catalog-audit.md`.

## Team Sequence

Complete each team fully before advancing, following `SeasonCatalog.cs` order:

1. [x] 清水建設江東ブルーシャークス (137, DIV2)
2. [x] 豊田自動織機シャトルズ愛知 (138, DIV2)
3. [x] 花園近鉄ライナーズ (140, DIV2)
4. [x] レッドハリケーンズ大阪 (142, DIV2)
5. [x] マツダスカイアクティブズ広島 (146, DIV2)
6. [x] JR東日本グリーンウォリアーズ東葛 (135, DIV2)
7. [x] 九州電力キューデンヴォルテクス (136, DIV2)
8. [x] クリタウォーターガッシュ昭島 (143, DIV3)
9. [x] 日野レッドドルフィンズ (141, DIV3)
10. [x] 日本製鉄釜石シーウェイブス (139, DIV3)
11. [x] 中国電力レッドレグリオンズ (145, DIV3)
12. [x] 川越狭山セコムラガッツ (144, DIV2)
13. [x] ルリーロ福岡 (148, DIV3)
14. [ ] ヤクルトレビンズ戸田 (147, DIV3) — 50名の名前/ポジション/DOB/身長/体重と高校情報を公式資料で照合。48名の測定値は2025-26登録表から補完し、今季プロフィール値は公式サイト停止中のため未確認。
15. [x] 丸和MOMOTARO’S成田 (149, DIV3)

For every team, record official roster/profile URLs, roster count, catalog count, confirmed additions/removals/corrections with player IDs and fields, duplicate decisions, and unresolved source conflicts in the audit report.

## Final Verification

- [x] Re-count per division/team; all edits made in this continuation were scoped to Div2/Div3 player records. Final counts: Div2 422; Div3 363; Div1 660.
- [x] Run `python tools/validate_player_catalog.py`; zero errors and zero review warnings.
- [x] Preview deduplication and normalization; both reported zero changes, so no apply was needed.
- [ ] Run relevant catalog tests and `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore` if environment permits.
- [x] Review the data edits and provenance; `git diff --check` reported no whitespace errors. Existing unrelated workspace changes were preserved.
- [x] Record team counts, evidence, material changes, and unresolved limits in the report. No commit, publish, or GitHub Actions run.
